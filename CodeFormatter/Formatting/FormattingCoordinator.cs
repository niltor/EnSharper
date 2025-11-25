using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        /// </summary>
        /// <param name="textView">The text view to format</param>
        /// <param name="serviceProvider">VS service provider</param>
        /// <param name="alignService">Alignment service</param>
        /// <param name="includeIDEFormatting">If true, applies IDE formatting + custom alignment in single edit.
        /// If false, only applies custom alignment (assumes IDE already formatted).</param>
        public static bool TryFormat(
            IWpfTextView textView,
            SVsServiceProvider serviceProvider,
            AlignService alignService,
            bool includeIDEFormatting = false
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
                var text = snapshot.GetText();

                // Get the workspace from the text buffer for proper formatting
                Workspace workspace = null;
                if (includeIDEFormatting)
                {
                    workspace = GetWorkspaceFromTextBuffer(textView.TextBuffer, serviceProvider);
                }

                var formattedText = alignService.FormatCode(text, skipRoslynFormatting: !includeIDEFormatting, workspace: workspace);

                bool isEqual = formattedText == text;
                Logger.LogDebug(
                    "FormattingCoordinator",
                    $"Formatted text length: {formattedText.Length}, Equal: {isEqual}"
                );

                if (isEqual)
                {
                    Logger.LogDebug("FormattingCoordinator", "No changes needed - skipping edit");
                    return false;
                }

                var logMessage = includeIDEFormatting
                    ? "Applying combined IDE formatting + custom alignment in single edit"
                    : "Applying custom alignment only";
                Logger.LogDebug("FormattingCoordinator", logMessage);

                using (var edit = textView.TextBuffer.CreateEdit())
                {
                    if (edit.Snapshot != snapshot)
                    {
                        Logger.LogDebug(
                            "FormattingCoordinator",
                            "Snapshot changed before edit could be applied"
                        );
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
                Logger.LogError("FormattingCoordinator.TryFormat", ex.ToString());
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
        /// Attempts to get the Roslyn Document for the current text buffer.
        /// </summary>
        private static async Task<Document> GetDocumentFromTextBufferAsync(
            ITextBuffer textBuffer, 
            SVsServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            try
            {
                var workspace = GetWorkspaceFromTextBuffer(textBuffer, serviceProvider);
                if (workspace == null)
                {
                    Logger.LogDebug("FormattingCoordinator", "No workspace available for document");
                    return null;
                }

                // Get the document from the current snapshot
                var snapshot = textBuffer.CurrentSnapshot;
                var textContainer = snapshot.AsText().Container;
                
                // Try to find the document in the workspace using the text container
                Document document = null;
                
                // First try: Use the extension method if available (requires Microsoft.CodeAnalysis.Workspaces)
                var documentId = workspace.GetDocumentIdInCurrentContext(textContainer);
                if (documentId != null)
                {
                    document = workspace.CurrentSolution.GetDocument(documentId);
                }
                
                // Fallback: Iterate through all documents to find one with matching text container
                // This is a slow path - add limits to prevent excessive blocking
                if (document == null)
                {
                    Logger.LogDebug("FormattingCoordinator", "Using fallback document search (may be slow for large solutions)");
                    
                    const int maxDocumentsToCheck = 100; // Limit to prevent UI thread blocking
                    int documentsChecked = 0;
                    bool limitReached = false;
                    
                    foreach (var project in workspace.CurrentSolution.Projects)
                    {
                        foreach (var doc in project.Documents)
                        {
                            if (documentsChecked >= maxDocumentsToCheck)
                            {
                                Logger.LogDebug("FormattingCoordinator", $"Reached maximum document check limit ({maxDocumentsToCheck})");
                                limitReached = true;
                                break;
                            }
                            
                            var docText = await doc.GetTextAsync(cancellationToken).ConfigureAwait(false);
                            if (docText?.Container == textContainer)
                            {
                                document = doc;
                                Logger.LogDebug("FormattingCoordinator", $"Found document via fallback after checking {documentsChecked + 1} documents");
                                break;
                            }
                            
                            documentsChecked++;
                        }
                        
                        // Exit outer loop if we found the document or reached the limit
                        if (document != null || limitReached) break;
                    }
                }

                if (document != null)
                {
                    Logger.LogDebug("FormattingCoordinator", $"Found document: {document.Name}");
                    return document;
                }
                else
                {
                    Logger.LogDebug("FormattingCoordinator", "Could not find document in workspace");
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug("FormattingCoordinator", $"Could not obtain Document: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Formats the document using the new document-based approach with minimal text changes.
        /// This avoids the flickering issue by only applying differential changes.
        /// </summary>
        /// <param name="textView">The text view to format</param>
        /// <param name="serviceProvider">VS service provider</param>
        /// <param name="alignService">Alignment service</param>
        /// <param name="includeIDEFormatting">If true, applies IDE formatting + custom alignment.
        /// If false, only applies custom alignment.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async Task<bool> TryFormatAsync(
            IWpfTextView textView,
            SVsServiceProvider serviceProvider,
            AlignService alignService,
            bool includeIDEFormatting = false,
            CancellationToken cancellationToken = default
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

                // Try to get the document from the workspace
                var document = await GetDocumentFromTextBufferAsync(textView.TextBuffer, serviceProvider, cancellationToken);
                
                if (document == null)
                {
                    Logger.LogDebug("FormattingCoordinator", "No document available, falling back to string-based formatting");
                    // Fall back to the old string-based approach
                    return TryFormat(textView, serviceProvider, alignService, includeIDEFormatting);
                }

                var logMessage = includeIDEFormatting
                    ? "Applying combined IDE formatting + custom alignment using document-based approach"
                    : "Applying custom alignment only using document-based approach";
                Logger.LogDebug("FormattingCoordinator", logMessage);

                // Format the document
                var formattedDocument = await alignService.FormatDocumentAsync(
                    document, 
                    skipRoslynFormatting: !includeIDEFormatting, 
                    cancellationToken);

                // Get text changes
                var oldText = await document.GetTextAsync(cancellationToken);
                var newText = await formattedDocument.GetTextAsync(cancellationToken);
                var changes = newText.GetTextChanges(oldText);

                if (changes.Count == 0)
                {
                    Logger.LogDebug("FormattingCoordinator", "No changes needed - skipping edit");
                    return false;
                }

                Logger.LogDebug("FormattingCoordinator", $"Applying {changes.Count} text changes");

                // Apply changes to the text buffer
                using (var edit = textView.TextBuffer.CreateEdit())
                {
                    // Verify buffer hasn't changed since we started formatting
                    if (textView.TextBuffer.CurrentSnapshot != snapshot)
                    {
                        Logger.LogDebug(
                            "FormattingCoordinator",
                            "Buffer was modified during formatting - aborting to avoid conflicts"
                        );
                        edit.Cancel();
                        return false;
                    }

                    // Apply each text change in descending order to avoid position shifts
                    // TextChanges from Roslyn are guaranteed to be non-overlapping
                    foreach (var change in changes.OrderByDescending(c => c.Span.Start))
                    {
                        var span = new Span(change.Span.Start, change.Span.Length);
                        edit.Replace(span, change.NewText);
                    }

                    var newSnapshot = edit.Apply();
                    Logger.LogDebug("FormattingCoordinator", "Text changes applied successfully");

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
                Logger.LogError("FormattingCoordinator.TryFormatAsync", ex.ToString());
                return false;
            }
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
