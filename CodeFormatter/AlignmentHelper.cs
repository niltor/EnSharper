using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;

namespace CodeFormatter
{
    /// <summary>
    /// Helper class for applying alignment formatting
    /// </summary>
    internal static class AlignmentHelper
    {
        // Search range for finding cursor line after formatting (lines above and below)
        private const int CursorSearchRange = 10;

        /// <summary>
        /// Applies alignment formatting to the text view if enabled
        /// </summary>
        /// <param name="textView">The text view to format</param>
        /// <param name="serviceProvider">The service provider</param>
        /// <param name="alignService">The alignment service</param>
        /// <param name="checkFormatOnSave">Whether to check the FormatOnSave option (for save events)</param>
        public static void ApplyAlignment(
            IWpfTextView textView,
            SVsServiceProvider serviceProvider,
            AlignService alignService,
            bool checkFormatOnSave = false)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                // Get options
                var shell = serviceProvider.GetService(typeof(SVsShell)) as IVsShell;
                if (shell == null)
                    return;

                // Try to get the package
                var packageGuid = new Guid(CodeFormatterPackage.PackageGuidString);
                IVsPackage package;

                // Try to get the package if it's already loaded
                int hr = shell.IsPackageLoaded(ref packageGuid, out package);
                if (hr != VSConstants.S_OK)
                    return;

                // If not loaded, load it
                if (package == null)
                {
                    hr = shell.LoadPackage(ref packageGuid, out package);
                    if (hr != VSConstants.S_OK)
                        return;
                }

                if (package is CodeFormatterPackage formatterPackage)
                {
                    var options = formatterPackage.GetDialogPage(typeof(AlignOptions)) as AlignOptions;

                    // Check if plugin is enabled
                    if (options == null || !options.EnablePlugin)
                        return;

                    // For save events, also check if FormatOnSave is enabled
                    if (checkFormatOnSave && !options.FormatOnSave)
                        return;

                    // Save cursor position before formatting
                    // We use line number and column offset which is simple and works well for alignment changes
                    // that preserve line structure. More complex tracking would be needed for refactorings
                    // that move code across lines, but that's not the case for alignment.
                    var caretPosition = textView.Caret.Position.BufferPosition;
                    var caretLine = caretPosition.GetContainingLine().LineNumber;
                    var caretColumn = caretPosition.Position - caretPosition.GetContainingLine().Start.Position;

                    // Get the current snapshot and text
                    var snapshot = textView.TextBuffer.CurrentSnapshot;
                    var text = snapshot.GetText();

                    // Format the code (sorting removed)
                    var formattedText = alignService.FormatCode(text);

                    // Only apply changes if text actually changed
                    if (formattedText != text)
                    {
                        // Apply the changes using the same snapshot we read from
                        var edit = textView.TextBuffer.CreateEdit();
                        
                        // Verify snapshot hasn't changed
                        if (edit.Snapshot == snapshot)
                        {
                            edit.Replace(0, snapshot.Length, formattedText);
                            var newSnapshot = edit.Apply();

                            // Restore cursor position
                            // Note: When sorting is enabled, we try to find the original line content
                            // If the line moved, we restore to the new position. Otherwise, use line number.
                            try
                            {
                                // Get the original caret line content before formatting
                                var originalLineContent = snapshot.GetLineFromLineNumber(caretLine).GetText();

                                // Get the new snapshot after edit
                                int targetLineNumber = -1;
                                if (newSnapshot != null)
                                {
                                    // Optimize search: check original position first, then search nearby lines
                                    // Most formatting operations don't move lines far
                                    int searchStart = Math.Max(0, caretLine - CursorSearchRange);
                                    int searchEnd = Math.Min(newSnapshot.LineCount, caretLine + CursorSearchRange);
                                    
                                    // First check the original line number if it exists
                                    if (caretLine < newSnapshot.LineCount)
                                    {
                                        var line = newSnapshot.GetLineFromLineNumber(caretLine);
                                        if (line.GetText() == originalLineContent)
                                        {
                                            targetLineNumber = caretLine;
                                        }
                                    }
                                    
                                    // If not found, search nearby lines
                                    if (targetLineNumber == -1)
                                    {
                                        for (int i = searchStart; i < searchEnd; i++)
                                        {
                                            var line = newSnapshot.GetLineFromLineNumber(i);
                                            if (line.GetText() == originalLineContent)
                                            {
                                                targetLineNumber = i;
                                                break;
                                            }
                                        }
                                    }

                                    // If found, restore caret to same column in that line
                                    if (targetLineNumber != -1)
                                    {
                                        var newLine = newSnapshot.GetLineFromLineNumber(targetLineNumber);
                                        var newPosition = Math.Min(newLine.Start.Position + caretColumn, newLine.End.Position);
                                        textView.Caret.MoveTo(new Microsoft.VisualStudio.Text.SnapshotPoint(newSnapshot, newPosition));
                                    }
                                    // If not found, fall back to previous logic (by line number)
                                    else if (caretLine < newSnapshot.LineCount)
                                    {
                                        var newLine = newSnapshot.GetLineFromLineNumber(caretLine);
                                        var newPosition = Math.Min(newLine.Start.Position + caretColumn, newLine.End.Position);
                                        textView.Caret.MoveTo(new Microsoft.VisualStudio.Text.SnapshotPoint(newSnapshot, newPosition));
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // If cursor restoration fails, log but don't crash
                                System.Diagnostics.Debug.WriteLine($"Failed to restore cursor position: {ex.Message}");
                            }
                        }
                        else
                        {
                            // Snapshot changed, cancel the edit
                            edit.Cancel();
                            System.Diagnostics.Debug.WriteLine("AlignmentHelper: Snapshot changed during formatting, skipping alignment");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error to ActivityLog and Debug output, but don't crash
                ActivityLog.LogError("CodeFormatter.AlignmentHelper", ex.ToString());
                System.Diagnostics.Debug.WriteLine($"Error in AlignmentHelper.ApplyAlignment: {ex}");
            }
        }
    }
}
