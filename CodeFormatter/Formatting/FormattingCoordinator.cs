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

        public static bool TryFormat(IWpfTextView textView, SVsServiceProvider serviceProvider, AlignService alignService)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (textView == null || serviceProvider == null || alignService == null)
                return false;

            try
            {
                var options = AlignServiceFactory.GetAlignOptions(serviceProvider);
                if (options == null || !options.EnablePlugin)
                    return false;

                if (!options.FormatOnSave)
                    return false;

                var caret = textView.Caret.Position.BufferPosition;
                var caretLine = caret.GetContainingLine().LineNumber;
                var caretColumn = caret.Position - caret.GetContainingLine().Start.Position;

                var viewportTop = textView.TextViewLines.FirstVisibleLine.Start;
                var topLine = viewportTop.GetContainingLine().LineNumber;
                var topLineOffset = viewportTop.Position - viewportTop.GetContainingLine().Start.Position;

                var snapshot = textView.TextBuffer.CurrentSnapshot;
                var text = snapshot.GetText();

                var formattedText = alignService.FormatCode(text);
                if (formattedText == text)
                    return false;

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
    }
}
