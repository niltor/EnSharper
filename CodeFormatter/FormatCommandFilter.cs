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

            try
            {
                System.Diagnostics.Debug.WriteLine("[CodeFormatter] AddFilterToView - Creating filter");

                var filter = new FormatCommandFilter(textView, serviceProvider);

                var viewAdapter = GetViewAdapter(textView, serviceProvider);
                if (viewAdapter != null)
                {
                    int hr = viewAdapter.AddCommandFilter(filter, out filter.nextCommandTarget);
                    if (hr == VSConstants.S_OK)
                    {
                        System.Diagnostics.Debug.WriteLine("[CodeFormatter] AddFilterToView - Command filter added successfully");
                        ActivityLog.LogInformation("CodeFormatter.FormatCommandFilter", "Format command filter installed successfully");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[CodeFormatter] AddFilterToView - Failed to add command filter. HRESULT: {hr}");
                        ActivityLog.LogWarning("CodeFormatter.FormatCommandFilter", $"Failed to add command filter. HRESULT: {hr}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[CodeFormatter] AddFilterToView - ViewAdapter is null");
                    ActivityLog.LogWarning("CodeFormatter.FormatCommandFilter", "Could not get view adapter - command filter not installed");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CodeFormatter] ERROR in AddFilterToView: {ex}");
                ActivityLog.LogError("CodeFormatter.FormatCommandFilter", $"Error adding filter to view: {ex}");
            }
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
                System.Diagnostics.Debug.WriteLine("[CodeFormatter] Format Document command detected");
                ActivityLog.LogInformation("CodeFormatter.FormatCommandFilter", "Format Document command intercepted");

                // Execute the original format command first
                int result =
                    nextCommandTarget?.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut)
                    ?? VSConstants.S_OK;

                System.Diagnostics.Debug.WriteLine($"[CodeFormatter] Original format completed with result: {result}");

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
            System.Diagnostics.Debug.WriteLine("[CodeFormatter] ApplyAlignment - Starting alignment");
            AlignmentHelper.ApplyAlignment(textView, serviceProvider, alignService, checkFormatOnSave: false);
            System.Diagnostics.Debug.WriteLine("[CodeFormatter] ApplyAlignment - Alignment complete");
        }
    }
}
