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

                    // Check if alignment is enabled
                    if (options == null || !options.EnablePlugin || !options.EnableAlign)
                        return;

                    // For save events, also check if FormatOnSave is enabled
                    if (checkFormatOnSave && !options.FormatOnSave)
                        return;

                    // Get the current snapshot and text
                    var snapshot = textView.TextBuffer.CurrentSnapshot;
                    var text = snapshot.GetText();

                    // Format the code
                    var formattedText = alignService.FormatCode(text);

                    if (formattedText != text)
                    {
                        // Apply the changes using the same snapshot we read from
                        var edit = textView.TextBuffer.CreateEdit();
                        // Verify snapshot hasn't changed
                        if (edit.Snapshot == snapshot)
                        {
                            edit.Replace(0, snapshot.Length, formattedText);
                            edit.Apply();
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
