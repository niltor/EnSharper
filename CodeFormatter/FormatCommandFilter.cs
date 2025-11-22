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
        // Use observed command id for Format Document in this VS version
        private const uint ECMD_FORMATDOCUMENT = 1990;

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

            Logger.LogError("👌 FormatCommandFilter.Exec", $"format command: pguid={pguidCmdGroup}, id={nCmdID}，execopt:{nCmdexecopt}");

            // Record snapshot version before executing the command to detect real buffer changes
            int beforeVersion = -1;
            try
            {
                if (textView?.TextBuffer?.CurrentSnapshot != null)
                    beforeVersion = textView.TextBuffer.CurrentSnapshot.Version.VersionNumber;
            }
            catch (Exception ex)
            {
                Logger.LogError("FormatCommandFilter.Exec", $"Failed reading snapshot version before exec: {ex}");
            }

            bool isFormatCommand = false;

            if (pguidCmdGroup == VSConstants.VSStd2K && nCmdID == ECMD_FORMATDOCUMENT)
            {
                isFormatCommand = true;
                Logger.LogDebug("FormatCommandFilter.Exec", $"Detected format command: pguid={pguidCmdGroup}, id={nCmdID}");
            }

            // Execute the original command
            int result = VSConstants.S_OK;
            if (nextCommandTarget != null)
            {
                try
                {
                    result = nextCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                }
                catch (Exception ex)
                {
                    Logger.LogError("FormatCommandFilter.Exec", $"nextCommandTarget.Exec threw: {ex}");
                }
            }

            // Check if buffer actually changed
            int afterVersion = -1;
            try
            {
                if (textView?.TextBuffer?.CurrentSnapshot != null)
                    afterVersion = textView.TextBuffer.CurrentSnapshot.Version.VersionNumber;
            }
            catch (Exception ex)
            {
                Logger.LogError("FormatCommandFilter.Exec", $"Failed reading snapshot version after exec: {ex}");
            }

            bool bufferChanged = beforeVersion != -1 && afterVersion != -1 && beforeVersion != afterVersion;
            Logger.LogDebug("FormatCommandFilter.Exec", $"Snapshot versions: before={beforeVersion}, after={afterVersion}, changed={bufferChanged}");

            if (isFormatCommand && bufferChanged)
            {
                ApplyAlignment();
            }

            return result;
        }

        private void ApplyAlignment()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                Logger.LogDebug("FormatCommandFilter.ApplyAlignment", "Calling AlignmentHelper.ApplyAlignment");
                AlignmentHelper.ApplyAlignment(textView, serviceProvider, alignService, checkFormatOnSave: false);
                Logger.LogDebug("FormatCommandFilter.ApplyAlignment", "Alignment applied successfully via format command");
            }
            catch (Exception ex)
            {
                Logger.LogError("FormatCommandFilter.ApplyAlignment", ex.ToString());
            }
        }
    }
}
