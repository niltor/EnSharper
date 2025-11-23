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
        private readonly IWpfTextView textView;
        private readonly SVsServiceProvider serviceProvider;
        private IOleCommandTarget nextCommandTarget;
        internal static readonly object AlignmentAppliedKey = new object();

        private FormatCommandFilter(IWpfTextView textView, SVsServiceProvider serviceProvider)
        {
            this.textView = textView;
            this.serviceProvider = serviceProvider;
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

            Logger.LogDebug("FormatCommandFilter", $"Added filter for view: {textView?.ToString()}");
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
                Logger.LogError("FormatCommandFilter.GetViewAdapter", ex.ToString());
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

            if (pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (int)VSConstants.VSStd2KCmdID.FORMATDOCUMENT)
            {
                isFormatCommand = true;
                Logger.LogDebug("FormatCommandFilter.Exec", $"Detected format command: pguid={pguidCmdGroup}, id={nCmdID}");
            }

            if (isFormatCommand)
            {
                int hr = VSConstants.S_OK;

                if (nextCommandTarget != null)
                {
                    hr = nextCommandTarget.Exec(
                        ref pguidCmdGroup,
                        nCmdID,
                        nCmdexecopt,
                        pvaIn,
                        pvaOut
                    );
                }
                else
                {
                    Logger.LogDebug("FormatCommandFilter.Exec", "No next command target available; running custom alignment only");
                }

                try
                {
                    ApplyAlignment(alignOnly: true);
                    Logger.LogDebug("FormatCommandFilter.Exec", "Alignment applied after default formatting");
                    return hr;
                }
                catch (Exception ex)
                {
                    Logger.LogError("FormatCommandFilter.Exec", $"Failed to apply alignment: {ex}");
                    return VSConstants.E_FAIL;
                }
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

        private void ApplyAlignment(bool alignOnly)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                Logger.LogDebug("FormatCommandFilter.ApplyAlignment", "Calling AlignmentHelper.ApplyAlignment");
                
                // Create AlignService with current options (reads latest configuration)
                var alignService = AlignServiceFactory.CreateFromOptions(serviceProvider);
                
                AlignmentHelper.ApplyAlignment(textView, serviceProvider, alignService, checkFormatOnSave: false, alignOnly: alignOnly);
                Logger.LogDebug("FormatCommandFilter.ApplyAlignment", "Alignment applied successfully via format command");

                textView.Properties[AlignmentAppliedKey] = true;
            }
            catch (Exception ex)
            {
                Logger.LogError("FormatCommandFilter.ApplyAlignment", ex.ToString());
            }
        }
    }
}
