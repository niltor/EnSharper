using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace CodeFormatter
{
    /// <summary>
    /// Command filter to intercept format document commands
    /// </summary>
    internal sealed class FormatCommandFilter : IOleCommandTarget
    {
        private const uint ECMD_FORMATDOCUMENT = 84;

        private readonly IWpfTextView textView;
        private readonly SVsServiceProvider serviceProvider;
        private readonly AlignService alignService;
        private IOleCommandTarget nextCommandTarget;

        private FormatCommandFilter(IWpfTextView textView, SVsServiceProvider serviceProvider)
        {
            this.textView = textView;
            this.serviceProvider = serviceProvider;
            this.alignService = new AlignService();
        }

        public static void AddFilterToView(
            IWpfTextView textView,
            SVsServiceProvider serviceProvider
        )
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var filter = new FormatCommandFilter(textView, serviceProvider);

            var viewAdapter = GetViewAdapter(textView, serviceProvider);
            viewAdapter?.AddCommandFilter(filter, out filter.nextCommandTarget);
        }

        private static IVsTextView GetViewAdapter(
            IWpfTextView textView,
            SVsServiceProvider serviceProvider
        )
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var componentModel =
                    serviceProvider.GetService(
                        typeof(Microsoft.VisualStudio.ComponentModelHost.SComponentModel)
                    ) as Microsoft.VisualStudio.ComponentModelHost.IComponentModel;

                if (componentModel == null)
                    return null;

                var editorAdapterFactory =
                    componentModel.GetService<Microsoft.VisualStudio.Editor.IVsEditorAdaptersFactoryService>();
                return editorAdapterFactory?.GetViewAdapter(textView);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in GetViewAdapter: {ex}");
                return null;
            }
        }

        public int QueryStatus(
            ref Guid pguidCmdGroup,
            uint cCmds,
            OLECMD[] prgCmds,
            IntPtr pCmdText
        )
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (nextCommandTarget != null)
            {
                return nextCommandTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
            }
            return VSConstants.E_FAIL;
        }

        public int Exec(
            ref Guid pguidCmdGroup,
            uint nCmdID,
            uint nCmdexecopt,
            IntPtr pvaIn,
            IntPtr pvaOut
        )
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Check if this is a format document command
            // VSStd2K command group, Format Document command ID
            if (pguidCmdGroup == VSConstants.VSStd2K && nCmdID == ECMD_FORMATDOCUMENT)
            {
                // Execute the original format command first
                int result =
                    nextCommandTarget?.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut)
                    ?? VSConstants.S_OK;

                // Then apply our alignment
                ApplyAlignment();

                return result;
            }

            // Pass other commands through
            if (nextCommandTarget != null)
            {
                return nextCommandTarget.Exec(
                    ref pguidCmdGroup,
                    nCmdID,
                    nCmdexecopt,
                    pvaIn,
                    pvaOut
                );
            }

            return VSConstants.E_FAIL;
        }

        private void ApplyAlignment()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                // Get options
                var shell = serviceProvider.GetService(typeof(SVsShell)) as IVsShell;
                if (shell == null)
                    return;

                // Try to get the package, loading it if necessary
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
                    var options =
                        formatterPackage.GetDialogPage(typeof(AlignOptions)) as AlignOptions;

                    // Check if alignment is enabled
                    if (options == null || !options.EnablePlugin || !options.EnableAlign)
                        return;

                    // Get the current text
                    var snapshot = textView.TextBuffer.CurrentSnapshot;
                    var text = snapshot.GetText();

                    // Format the code
                    var formattedText = alignService.FormatCode(text);

                    if (formattedText != text)
                    {
                        // Apply the changes
                        var edit = textView.TextBuffer.CreateEdit();
                        edit.Replace(0, snapshot.Length, formattedText);
                        edit.Apply();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error to ActivityLog and Debug output, but don't crash
                ActivityLog.LogError(nameof(FormatCommandFilter), ex.ToString());
                System.Diagnostics.Debug.WriteLine($"Error in FormatCommandFilter: {ex}");
            }
        }
    }
}
