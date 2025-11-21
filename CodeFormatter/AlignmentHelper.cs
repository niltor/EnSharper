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
                System.Diagnostics.Debug.WriteLine($"[CodeFormatter] ApplyAlignment - checkFormatOnSave={checkFormatOnSave}");

                // Get options
                var shell = serviceProvider.GetService(typeof(SVsShell)) as IVsShell;
                if (shell == null)
                {
                    System.Diagnostics.Debug.WriteLine("[CodeFormatter] ApplyAlignment - Shell service is null");
                    ActivityLog.LogWarning("CodeFormatter.AlignmentHelper", "Could not get shell service");
                    return;
                }

                // Try to get the package
                var packageGuid = new Guid(CodeFormatterPackage.PackageGuidString);
                IVsPackage package;

                // Try to get the package if it's already loaded
                int hr = shell.IsPackageLoaded(ref packageGuid, out package);
                System.Diagnostics.Debug.WriteLine($"[CodeFormatter] ApplyAlignment - IsPackageLoaded result: {hr}, package={(package == null ? "null" : "loaded")}");
                
                if (hr != VSConstants.S_OK)
                {
                    System.Diagnostics.Debug.WriteLine("[CodeFormatter] ApplyAlignment - Package not loaded, attempting to load");
                    ActivityLog.LogInformation("CodeFormatter.AlignmentHelper", "Package not loaded, attempting to load it");
                }

                // If not loaded, load it
                if (package == null)
                {
                    hr = shell.LoadPackage(ref packageGuid, out package);
                    System.Diagnostics.Debug.WriteLine($"[CodeFormatter] ApplyAlignment - LoadPackage result: {hr}");
                    
                    if (hr != VSConstants.S_OK)
                    {
                        ActivityLog.LogError("CodeFormatter.AlignmentHelper", $"Failed to load package. HRESULT: {hr}");
                        return;
                    }
                }

                if (package is CodeFormatterPackage formatterPackage)
                {
                    var options = formatterPackage.GetDialogPage(typeof(AlignOptions)) as AlignOptions;

                    if (options == null)
                    {
                        System.Diagnostics.Debug.WriteLine("[CodeFormatter] ApplyAlignment - Options is null");
                        ActivityLog.LogWarning("CodeFormatter.AlignmentHelper", "Could not get options page");
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine($"[CodeFormatter] ApplyAlignment - Options: EnablePlugin={options.EnablePlugin}, EnableAlign={options.EnableAlign}, FormatOnSave={options.FormatOnSave}");

                    // Check if alignment is enabled
                    if (!options.EnablePlugin || !options.EnableAlign)
                    {
                        System.Diagnostics.Debug.WriteLine("[CodeFormatter] ApplyAlignment - Plugin or alignment is disabled");
                        ActivityLog.LogInformation("CodeFormatter.AlignmentHelper", $"Alignment skipped: EnablePlugin={options.EnablePlugin}, EnableAlign={options.EnableAlign}");
                        return;
                    }

                    // For save events, also check if FormatOnSave is enabled
                    if (checkFormatOnSave && !options.FormatOnSave)
                    {
                        System.Diagnostics.Debug.WriteLine("[CodeFormatter] ApplyAlignment - FormatOnSave is disabled");
                        ActivityLog.LogInformation("CodeFormatter.AlignmentHelper", "Format on save is disabled");
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine("[CodeFormatter] ApplyAlignment - Starting formatting");

                    // Get the current snapshot and text
                    var snapshot = textView.TextBuffer.CurrentSnapshot;
                    var text = snapshot.GetText();

                    System.Diagnostics.Debug.WriteLine($"[CodeFormatter] ApplyAlignment - Text length: {text.Length}");

                    // Format the code
                    var formattedText = alignService.FormatCode(text);

                    if (formattedText != text)
                    {
                        System.Diagnostics.Debug.WriteLine("[CodeFormatter] ApplyAlignment - Text changed, applying edits");
                        ActivityLog.LogInformation("CodeFormatter.AlignmentHelper", "Applying alignment changes");

                        // Apply the changes using the same snapshot we read from
                        var edit = textView.TextBuffer.CreateEdit();
                        // Verify snapshot hasn't changed
                        if (edit.Snapshot == snapshot)
                        {
                            edit.Replace(0, snapshot.Length, formattedText);
                            edit.Apply();
                            System.Diagnostics.Debug.WriteLine("[CodeFormatter] ApplyAlignment - Edits applied successfully");
                        }
                        else
                        {
                            // Snapshot changed, cancel the edit
                            edit.Cancel();
                            System.Diagnostics.Debug.WriteLine("[CodeFormatter] ApplyAlignment - Snapshot changed, edit cancelled");
                            ActivityLog.LogWarning("CodeFormatter.AlignmentHelper", "Snapshot changed during formatting, skipping alignment");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[CodeFormatter] ApplyAlignment - No changes needed");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[CodeFormatter] ApplyAlignment - Package is not CodeFormatterPackage: {package?.GetType().FullName}");
                    ActivityLog.LogError("CodeFormatter.AlignmentHelper", "Package is not of expected type");
                }
            }
            catch (Exception ex)
            {
                // Log error to ActivityLog and Debug output, but don't crash
                ActivityLog.LogError("CodeFormatter.AlignmentHelper", ex.ToString());
                System.Diagnostics.Debug.WriteLine($"[CodeFormatter] ERROR in ApplyAlignment: {ex}");
            }
        }
    }
}
