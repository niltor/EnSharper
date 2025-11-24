using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace CodeFormatter
{
    /// <summary>
    /// Global singleton listener for document save events
    /// </summary>
    internal sealed class DocumentSaveListener : IVsRunningDocTableEvents3, IDisposable
    {
        private static DocumentSaveListener instance;
        private static readonly object lockObject = new object();

        private readonly SVsServiceProvider serviceProvider;
        private uint rdtCookie;
        private IVsRunningDocumentTable rdt;
        private bool isFormatting = false;
        private string lastFormattedText = string.Empty;


        private DocumentSaveListener(SVsServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public static DocumentSaveListener GetOrCreate(SVsServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            lock (lockObject)
            {
                if (instance == null)
                {
                    instance = new DocumentSaveListener(serviceProvider);
                    instance.Initialize();
                    Logger.LogDebug("GlobalDocumentSaveListener", "Global instance created");
                }
                return instance;
            }
        }

        private void Initialize()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var svc = serviceProvider.GetService(typeof(SVsRunningDocumentTable));
                if (svc == null)
                {
                    Logger.LogDebug(
                        "GlobalDocumentSaveListener",
                        "SVsRunningDocumentTable service not available"
                    );
                    return;
                }

                rdt = svc as IVsRunningDocumentTable;
                if (rdt != null)
                {
                    int hr = rdt.AdviseRunningDocTableEvents(this, out rdtCookie);
                    if (hr != VSConstants.S_OK)
                    {
                        Logger.LogDebug(
                            "GlobalDocumentSaveListener",
                            $"Failed to advise RDT events. HRESULT: {hr}"
                        );
                        rdtCookie = 0;
                    }
                    else
                    {
                        Logger.LogDebug("GlobalDocumentSaveListener", "Subscribed to RDT events");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("GlobalDocumentSaveListener.Initialize", ex.ToString());
            }
        }

        public int OnBeforeSave(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (isFormatting)
            {
                Logger.LogDebug("GlobalDocumentSaveListener", "Skipping - already formatting");
                return VSConstants.S_OK;
            }

            try
            {
                isFormatting = true;
                uint grfRDTFlags;
                uint dwReadLocks;
                uint dwEditLocks;
                string documentPath;
                IVsHierarchy ppHier;
                uint pitemid;
                IntPtr ppunkDocData = IntPtr.Zero;

                try
                {
                    int hr = rdt.GetDocumentInfo(
                        docCookie,
                        out grfRDTFlags,
                        out dwReadLocks,
                        out dwEditLocks,
                        out documentPath,
                        out ppHier,
                        out pitemid,
                        out ppunkDocData
                    );

                    if (hr != VSConstants.S_OK || string.IsNullOrEmpty(documentPath))
                    {
                        return VSConstants.S_OK;
                    }

                    // Check if this is a C# file
                    if (!documentPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        return VSConstants.S_OK;
                    }

                    Logger.LogDebug(
                        "GlobalDocumentSaveListener",
                        $"OnBeforeSave for {documentPath}"
                    );

                    var textView = GetActiveTextView();
                    if (textView == null)
                    {
                        Logger.LogDebug(
                            "GlobalDocumentSaveListener",
                            "No active text view - skipping format"
                        );
                        return VSConstants.S_OK;
                    }

                    // Verify the active view is the document being saved
                    var componentModel =
                        serviceProvider.GetService(
                            typeof(Microsoft.VisualStudio.ComponentModelHost.SComponentModel)
                        ) as Microsoft.VisualStudio.ComponentModelHost.IComponentModel;
                    var textDocumentFactory =
                        componentModel?.GetService<ITextDocumentFactoryService>();

                    if (textDocumentFactory == null || !textDocumentFactory.TryGetTextDocument(
                            textView.TextBuffer,
                            out var activeDoc
                        )
                    )
                    {
                        Logger.LogDebug(
                            "GlobalDocumentSaveListener",
                            "Cannot get document from active view"
                        );
                        return VSConstants.S_OK;
                    }

                    // Only format if the active document is the one being saved
                    if (
                        !string.Equals(
                            activeDoc.FilePath,
                            documentPath,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        Logger.LogDebug(
                            "GlobalDocumentSaveListener",
                            $"Skipping - active document ({activeDoc.FilePath}) is not the one being saved ({documentPath})"
                        );
                        return VSConstants.S_OK;
                    }

                    var textBuffer = textView.TextBuffer;
                    if (textBuffer == null)
                    {
                        return VSConstants.S_OK;
                    }

                    var currentText = textBuffer.CurrentSnapshot.GetText();

                    Logger.LogDebug("DocumentSaveListener", $"Current text: {currentText.Length}, last: {lastFormattedText.Length}");

                    if (lastFormattedText == currentText)
                    {
                        Logger.LogDebug(
                            "GlobalDocumentSaveListener",
                            "Text unchanged since last format - skipping"
                        );
                        return VSConstants.S_OK;
                    }

                    var alignService = AlignServiceFactory.CreateFromOptions(serviceProvider);
                    bool formatted = FormattingCoordinator.TryFormat(
                        textView,
                        serviceProvider,
                        alignService,
                        false
                    );

                    if (formatted)
                    {
                        // Remember formatted text to skip next save if unchanged
                        lastFormattedText = textBuffer.CurrentSnapshot.GetText();
                        Logger.LogDebug(
                            "GlobalDocumentSaveListener",
                            "Custom alignment applied on save"
                        );
                    }
                    else
                    {
                        lastFormattedText = currentText;
                        Logger.LogDebug(
                            "GlobalDocumentSaveListener",
                            "No alignment changes needed on save"
                        );
                    }
                }
                finally
                {
                    if (ppunkDocData != IntPtr.Zero)
                    {
                        System.Runtime.InteropServices.Marshal.Release(ppunkDocData);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("GlobalDocumentSaveListener.OnBeforeSave", ex.ToString());
            }
            finally
            {
                isFormatting = false;
            }

            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie)
        {
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Gets the currently active text view
        /// </summary>
        private IWpfTextView GetActiveTextView()
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
                if (editorAdapterFactory == null)
                    return null;

                var textManager =
                    serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager;
                if (textManager == null)
                    return null;

                IVsTextView vsTextView;
                int hr = textManager.GetActiveView(1, null, out vsTextView);
                if (hr != VSConstants.S_OK || vsTextView == null)
                    return null;

                return editorAdapterFactory.GetWpfTextView(vsTextView);
            }
            catch (Exception ex)
            {
                Logger.LogError("GlobalDocumentSaveListener.GetActiveTextView", ex.ToString());
                return null;
            }
        }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (rdt != null && rdtCookie != 0)
            {
                try
                {
                    rdt.UnadviseRunningDocTableEvents(rdtCookie);
                    rdtCookie = 0;
                    Logger.LogDebug("GlobalDocumentSaveListener", "Unsubscribed from RDT events");
                }
                catch (Exception ex)
                {
                    Logger.LogError("GlobalDocumentSaveListener.Dispose", ex.ToString());
                }
            }
        }

        #region Unused IVsRunningDocTableEvents3 methods

        public int OnAfterFirstDocumentLock(
            uint docCookie,
            uint dwRDTLockType,
            uint dwReadLocksRemaining,
            uint dwEditLocksRemaining
        )
        {
            Logger.LogDebug(
                "GlobalDocumentSaveListener",
                "OnAfterFirstDocumentLock called - not used"
            );
            return VSConstants.S_OK;
        }

        public int OnBeforeLastDocumentUnlock(
            uint docCookie,
            uint dwRDTLockType,
            uint dwReadLocksRemaining,
            uint dwEditLocksRemaining
        )
        {
            Logger.LogDebug(
                "GlobalDocumentSaveListener",
                "OnBeforeLastDocumentUnlock called - not used"
            );
            return VSConstants.S_OK;
        }

        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterAttributeChangeEx(
            uint docCookie,
            uint grfAttribs,
            IVsHierarchy pHierOld,
            uint itemidOld,
            string pszMkDocumentOld,
            IVsHierarchy pHierNew,
            uint itemidNew,
            string pszMkDocumentNew
        )
        {
            return VSConstants.S_OK;
        }

        #endregion
    }
}
