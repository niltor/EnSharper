using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace CodeFormatter
{
    /// <summary>
    /// Coordinates VS listeners with the alignment engine while preserving caret/viewport state.
    /// </summary>
    public static class FormattingCoordinator
    {
        private const int CursorSearchRange = 10;

        /// <summary>
        /// Formats the document with custom alignment and optionally IDE formatting.
        /// Uses Document-based API for differential updates to reduce flickering.
        /// </summary>
        /// <param name="textView">The text view to format</param>
        /// <param name="serviceProvider">VS service provider</param>
        /// <param name="alignService">Alignment service</param>
        /// <param name="includeIDFormatting">If true, applies IDE formatting + custom alignment in single edit.
        /// If false, only applies custom alignment (assumes IDE already formatted).</param>
        public static bool TryFormat(
            IWpfTextView textView,
            SVsServiceProvider serviceProvider,
            AlignService alignService,
            bool includeIDFormatting = false
        )
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (textView == null || serviceProvider == null || alignService == null)
                return false;

            try
            {
                var options = AlignServiceFactory.GetAlignOptions(serviceProvider);
                if (options == null || !options.EnablePlugin)
                {
                    Logger.LogDebug("FormattingCoordinator", "Plugin disabled");
                    return false;
                }

                var caret = textView.Caret.Position.BufferPosition;
                var caretLine = caret.GetContainingLine().LineNumber;
                var caretColumn = caret.Position - caret.GetContainingLine().Start.Position;

                var viewportTop = textView.TextViewLines.FirstVisibleLine.Start;
                var topLine = viewportTop.GetContainingLine().LineNumber;
                var topLineOffset =
                    viewportTop.Position - viewportTop.GetContainingLine().Start.Position;

                var snapshot = textView.TextBuffer.CurrentSnapshot;

                // Try to use Document-based approach for differential updates
                var workspace = GetWorkspaceFromTextBuffer(textView.TextBuffer, serviceProvider);
                if (workspace != null)
                {
                    var document = GetDocumentFromTextBuffer(textView.TextBuffer, workspace);
                    if (document != null)
                    {
                        Logger.LogDebug("FormattingCoordinator", "Using Document-based formatting with differential updates");
                        return TryFormatWithDocument(
                            textView,
                            document,
                            alignService,
                            includeIDFormatting,
                            snapshot,
                            caretLine,
                            caretColumn,
                            topLine,
                            topLineOffset
                        );
                    }
                }

                // Fallback to string-based formatting if Document is not available
                Logger.LogDebug("FormattingCoordinator", "Document not available, falling back to string-based formatting");
                return TryFormatWithString(
                    textView,
                    alignService,
                    includeIDFormatting,
                    workspace,
                    snapshot,
                    caretLine,
                    caretColumn,
                    topLine,
                    topLineOffset
                );
            }
            catch (Exception ex)
            {
                Logger.LogError("FormattingCoordinator.TryFormat", ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Formats using Document API with differential text changes to minimize flickering.
        /// </summary>
        private static bool TryFormatWithDocument(
            IWpfTextView textView,
            Document document,
            AlignService alignService,
            bool includeIDFormatting,
            ITextSnapshot snapshot,
            int caretLine,
            int caretColumn,
            int topLine,
            int topLineOffset
        )
        {
            try
            {
                // Format the document asynchronously
                // Note: We use ConfigureAwait(false) to avoid capturing the UI synchronization context
                var formattedDocument = alignService.FormatDocumentAsync(
                    document,
                    skipRoslynFormatting: !includeIDFormatting,
                    CancellationToken.None
                ).ConfigureAwait(false).GetAwaiter().GetResult();

                if (formattedDocument == document)
                {
                    Logger.LogDebug("FormattingCoordinator", "Document unchanged after formatting");
                    return false;
                }

                // Get text changes between original and formatted document
                var oldText = document.GetTextAsync(CancellationToken.None)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
                var newText = formattedDocument.GetTextAsync(CancellationToken.None)
                    .ConfigureAwait(false).GetAwaiter().GetResult();

                var changes = newText.GetTextChanges(oldText);
                if (changes == null || !changes.Any())
                {
                    Logger.LogDebug("FormattingCoordinator", "No text changes detected");
                    return false;
                }

                Logger.LogDebug("FormattingCoordinator", $"Applying {changes.Count()} differential text changes");

                // Apply the differential changes to the text buffer
                using (var edit = textView.TextBuffer.CreateEdit())
                {
                    if (edit.Snapshot != snapshot)
                    {
                        Logger.LogDebug("FormattingCoordinator", "Snapshot changed before edit could be applied");
                        edit.Cancel();
                        return false;
                    }

                    // Apply changes in reverse order to maintain correct positions
                    foreach (var change in changes.OrderByDescending(c => c.Span.Start))
                    {
                        var span = new Span(change.Span.Start, change.Span.Length);
                        edit.Replace(span, change.NewText);
                    }

                    var newSnapshot = edit.Apply();
                    Logger.LogDebug("FormattingCoordinator", "Differential text edits applied successfully");

                    RestoreCaretAndViewport(
                        textView,
                        snapshot,
                        newSnapshot,
                        caretLine,
                        caretColumn,
                        topLine,
                        topLineOffset
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("FormattingCoordinator.TryFormatWithDocument", ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Fallback formatting using string-based approach (full text replacement).
        /// </summary>
        private static bool TryFormatWithString(
            IWpfTextView textView,
            AlignService alignService,
            bool includeIDFormatting,
            Workspace workspace,
            ITextSnapshot snapshot,
            int caretLine,
            int caretColumn,
            int topLine,
            int topLineOffset
        )
        {
            try
            {
                var text = snapshot.GetText();
                Logger.LogDebug("FormattingCoordinator", $"Current text length: {text.Length}");

                var formattedText = alignService.FormatCode(
                    text,
                    skipRoslynFormatting: !includeIDFormatting,
                    workspace: workspace
                );

                bool isEqual = formattedText == text;
                Logger.LogDebug("FormattingCoordinator", $"Formatted text length: {formattedText.Length}, Equal: {isEqual}");

                if (isEqual)
                {
                    Logger.LogDebug("FormattingCoordinator", "No changes needed - skipping edit");
                    return false;
                }

                var logMessage = includeIDFormatting
                    ? "Applying combined IDE formatting + custom alignment (full text replacement)"
                    : "Applying custom alignment only (full text replacement)";
                Logger.LogDebug("FormattingCoordinator", logMessage);

                using (var edit = textView.TextBuffer.CreateEdit())
                {
                    if (edit.Snapshot != snapshot)
                    {
                        Logger.LogDebug("FormattingCoordinator", "Snapshot changed before edit could be applied");
                        edit.Cancel();
                        return false;
                    }

                    edit.Replace(0, snapshot.Length, formattedText);
                    var newSnapshot = edit.Apply();

                    Logger.LogDebug("FormattingCoordinator", "Text edit applied successfully");

                    RestoreCaretAndViewport(
                        textView,
                        snapshot,
                        newSnapshot,
                        caretLine,
                        caretColumn,
                        topLine,
                        topLineOffset
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("FormattingCoordinator.TryFormatWithString", ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Attempts to get the Roslyn workspace for the current text buffer.
        /// </summary>
        private static Workspace GetWorkspaceFromTextBuffer(ITextBuffer textBuffer, SVsServiceProvider serviceProvider)
        {
            try
            {
                var componentModel =
                    serviceProvider.GetService(
                        typeof(Microsoft.VisualStudio.ComponentModelHost.SComponentModel)
                    ) as Microsoft.VisualStudio.ComponentModelHost.IComponentModel;
                if (componentModel == null)
                    return null;

                // Try to get workspace from the buffer properties first (most direct method)
                if (textBuffer.Properties.TryGetProperty(typeof(Workspace), out Workspace bufferWorkspace) && bufferWorkspace != null)
                {
                    Logger.LogDebug("FormattingCoordinator", "Obtained Workspace from TextBuffer properties");
                    return bufferWorkspace;
                }

                // Fallback: Get the workspace service and find the workspace containing this buffer
                var workspaceService = componentModel.GetService<Microsoft.CodeAnalysis.Workspace>();
                if (workspaceService != null)
                {
                    Logger.LogDebug("FormattingCoordinator", "Obtained Workspace from Workspace service");
                    return workspaceService;
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug("FormattingCoordinator", $"Could not obtain Workspace: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Attempts to get the Roslyn Document for the current text buffer from the workspace.
        /// </summary>
        private static Document GetDocumentFromTextBuffer(ITextBuffer textBuffer, Workspace workspace)
        {
            if (workspace == null || textBuffer == null)
                return null;

            try
            {
                var currentSnapshot = textBuffer.CurrentSnapshot;
                var textContainer = currentSnapshot.TextBuffer.AsTextContainer();
                
                // Try to get the document ID from the text container
                var documentId = workspace.GetDocumentIdInCurrentContext(textContainer);
                if (documentId != null)
                {
                    var document = workspace.CurrentSolution.GetDocument(documentId);
                    if (document != null)
                    {
                        Logger.LogDebug("FormattingCoordinator", $"Obtained Document from Workspace: {document.Name}");
                        return document;
                    }
                }

                Logger.LogDebug("FormattingCoordinator", "Could not obtain Document from Workspace");
            }
            catch (Exception ex)
            {
                Logger.LogDebug("FormattingCoordinator", $"Error getting Document: {ex.Message}");
            }

            return null;
        }

        private static void RestoreCaretAndViewport(
            IWpfTextView textView,
            ITextSnapshot oldSnapshot,
            ITextSnapshot newSnapshot,
            int caretLine,
            int caretColumn,
            int topLine,
            int topLineOffset
        )
        {
            if (newSnapshot == null)
                return;

            try
            {
                var originalLineText =
                    caretLine < oldSnapshot.LineCount
                        ? oldSnapshot.GetLineFromLineNumber(caretLine).GetText()
                        : null;

                int targetLineNumber = -1;
                if (originalLineText != null)
                {
                    var searchStart = Math.Max(0, caretLine - CursorSearchRange);
                    var searchEnd = Math.Min(newSnapshot.LineCount, caretLine + CursorSearchRange);

                    if (
                        caretLine < newSnapshot.LineCount
                        && newSnapshot
                            .GetLineFromLineNumber(caretLine)
                            .GetText()
                            .Equals(originalLineText, StringComparison.Ordinal)
                    )
                    {
                        targetLineNumber = caretLine;
                    }
                    else
                    {
                        for (int i = searchStart; i < searchEnd; i++)
                        {
                            if (
                                newSnapshot
                                    .GetLineFromLineNumber(i)
                                    .GetText()
                                    .Equals(originalLineText, StringComparison.Ordinal)
                            )
                            {
                                targetLineNumber = i;
                                break;
                            }
                        }
                    }
                }

                if (targetLineNumber == -1 && caretLine < newSnapshot.LineCount)
                    targetLineNumber = caretLine;

                if (targetLineNumber != -1)
                {
                    var newLine = newSnapshot.GetLineFromLineNumber(targetLineNumber);
                    var newPosition = Math.Min(
                        newLine.Start.Position + caretColumn,
                        newLine.End.Position
                    );
                    textView.Caret.MoveTo(new SnapshotPoint(newSnapshot, newPosition));
                }

                if (topLine < newSnapshot.LineCount)
                {
                    var newTop = newSnapshot.GetLineFromLineNumber(topLine);
                    var newTopPosition = Math.Min(
                        newTop.Start.Position + topLineOffset,
                        newTop.End.Position
                    );
                    var topPoint = new SnapshotPoint(newSnapshot, newTopPosition);
                    textView.DisplayTextLineContainingBufferPosition(
                        topPoint,
                        0,
                        ViewRelativePosition.Top
                    );
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(
                    "FormattingCoordinator",
                    $"Failed to restore caret/viewport: {ex.Message}"
                );
            }
        }
    }
}
