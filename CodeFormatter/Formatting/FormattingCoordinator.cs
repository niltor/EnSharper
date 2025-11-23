using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
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
        /// Formats the document with custom alignment
        /// </summary>
        public static bool TryFormat(IWpfTextView textView, SVsServiceProvider serviceProvider, AlignService alignService)
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

                // FormatOnSave check removed - we only format when explicitly called
                // (via Format Document shortcut, not on save)

                var caret = textView.Caret.Position.BufferPosition;
                var caretLine = caret.GetContainingLine().LineNumber;
                var caretColumn = caret.Position - caret.GetContainingLine().Start.Position;

                var viewportTop = textView.TextViewLines.FirstVisibleLine.Start;
                var topLine = viewportTop.GetContainingLine().LineNumber;
                var topLineOffset = viewportTop.Position - viewportTop.GetContainingLine().Start.Position;

                var snapshot = textView.TextBuffer.CurrentSnapshot;
                var text = snapshot.GetText();

                Logger.LogDebug("FormattingCoordinator", $"Current text length: {text.Length}");
                
                // Always skip Roslyn formatting - we only do custom alignment
                // The IDE's formatter (which user invokes) handles standard formatting
                var formattedText = alignService.FormatCode(text, skipRoslynFormatting: true);
                
                bool isEqual = formattedText == text;
                Logger.LogDebug("FormattingCoordinator", $"Formatted text length: {formattedText.Length}, Equal: {isEqual}");
                
                if (isEqual)
                {
                    Logger.LogDebug("FormattingCoordinator", "No changes needed - skipping edit");
                    return false;
                }

                Logger.LogDebug("FormattingCoordinator", "Applying text edit");
                
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
                    
                    RestoreCaretAndViewport(textView, snapshot, newSnapshot, caretLine, caretColumn, topLine, topLineOffset);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("FormattingCoordinator", ex.ToString());
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
            int topLineOffset)
        {
            if (newSnapshot == null)
                return;

            try
            {
                var originalLineText = caretLine < oldSnapshot.LineCount
                    ? oldSnapshot.GetLineFromLineNumber(caretLine).GetText()
                    : null;

                int targetLineNumber = -1;
                if (originalLineText != null)
                {
                    var searchStart = Math.Max(0, caretLine - CursorSearchRange);
                    var searchEnd = Math.Min(newSnapshot.LineCount, caretLine + CursorSearchRange);

                    if (caretLine < newSnapshot.LineCount &&
                        newSnapshot.GetLineFromLineNumber(caretLine).GetText().Equals(originalLineText, StringComparison.Ordinal))
                    {
                        targetLineNumber = caretLine;
                    }
                    else
                    {
                        for (int i = searchStart; i < searchEnd; i++)
                        {
                            if (newSnapshot.GetLineFromLineNumber(i).GetText().Equals(originalLineText, StringComparison.Ordinal))
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
                    var newPosition = Math.Min(newLine.Start.Position + caretColumn, newLine.End.Position);
                    textView.Caret.MoveTo(new SnapshotPoint(newSnapshot, newPosition));
                }

                if (topLine < newSnapshot.LineCount)
                {
                    var newTop = newSnapshot.GetLineFromLineNumber(topLine);
                    var newTopPosition = Math.Min(newTop.Start.Position + topLineOffset, newTop.End.Position);
                    var topPoint = new SnapshotPoint(newSnapshot, newTopPosition);
                    textView.DisplayTextLineContainingBufferPosition(topPoint, 0, ViewRelativePosition.Top);
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug("FormattingCoordinator", $"Failed to restore caret/viewport: {ex.Message}");
            }
        }

        private static string EscapeChar(char c)
        {
            switch (c)
            {
                case '\r': return "\\r";
                case '\n': return "\\n";
                case '\t': return "\\t";
                case ' ': return "<space>";
                default: return c.ToString();
            }
        }

        private static string EscapeString(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "<empty>";
            
            if (s.Length > 50)
                s = s.Substring(0, 50) + "...";
            
            return s.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }
    }
}
