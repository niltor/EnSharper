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
        // VSStd2K commands - only handling Format Document, not Format Selection
        private const uint ECMD_FORMATDOCUMENT = 84;      // Ctrl+K, Ctrl+D
        
        // VSStd97 commands  
        private const uint cmdidFormatDocument = 247;     // Alternative format document command (Ctrl+Shift+F)

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

            bool isFormatCommand = false;

            // Check for various format document commands
            if (pguidCmdGroup == VSConstants.VSStd2K && nCmdID == ECMD_FORMATDOCUMENT)
            {
                isFormatCommand = true;
                System.Diagnostics.Debug.WriteLine($"Format command detected: VSStd2K command {nCmdID}");
            }
            else if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97 && nCmdID == cmdidFormatDocument)
            {
                isFormatCommand = true;
                System.Diagnostics.Debug.WriteLine($"Format command detected: VSStd97 command {nCmdID}");
            }

            if (isFormatCommand)
            {
                // Execute the original format command first
                int result = VSConstants.S_OK;
                if (nextCommandTarget != null)
                {
                    result = nextCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                }

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
                AlignmentHelper.ApplyAlignment(textView, serviceProvider, alignService, checkFormatOnSave: false);
                System.Diagnostics.Debug.WriteLine("Alignment applied successfully via format command");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying alignment: {ex}");
            }
        }
    }
}
