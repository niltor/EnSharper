using CodeFormatter.Services;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;

namespace CodeFormatter.Listeners
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
        private readonly Dictionary<string, string> lastFormattedContentByPath =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);


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
                    Logger.LogDebug("DocumentSaveListener", "Global instance created");
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
                        "DocumentSaveListener",
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
                            "DocumentSaveListener",
                            $"Failed to advise RDT events. HRESULT: {hr}"
                        );
                        rdtCookie = 0;
                    }
                    else
                    {
                        Logger.LogDebug("DocumentSaveListener", "Subscribed to RDT events");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("DocumentSaveListener.Initialize", ex.ToString());
            }
        }

        /// <summary>
        /// when enable code clean on save, ide formatter is called before this
        /// </summary>
        /// <param name="docCookie"></param>
        /// <returns></returns>
        public int OnBeforeSave(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (isFormatting)
            {
                Logger.LogDebug("DocumentSaveListener", "Skipping - already formatting");
                return VSConstants.S_OK;
            }
            try
            {
                isFormatting = true;
                var task = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    IWpfTextView textView = null;
                    var document = AlignService.GetActiveDocument(serviceProvider, out textView);
                    if (document.Project.Language == LanguageNames.CSharp && document.FilePath.EndsWith(".cs"))
                    {
                        var alignService = AlignServiceFactory.CreateFromOptions(serviceProvider);

                        if (alignService.IsEnabled)
                        {
                            Document formattedDoc = await alignService.FormatDocumentAsync(document);

                            if (formattedDoc != document)
                            {
                                var oldText = await document.GetTextAsync();
                                var newText = await formattedDoc.GetTextAsync();
                                var changes = newText.GetTextChanges(oldText);

                                using (var edit = textView.TextBuffer.CreateEdit())
                                {
                                    foreach (var change in changes)
                                    {
                                        edit.Replace(change.Span.Start, change.Span.Length, change.NewText);
                                    }
                                    edit.Apply();
                                }
                            }
                        }
                    }

                });
            }
            catch (Exception ex)
            {
                Logger.LogError("DocumentSaveListener.OnBeforeSave", ex.ToString());
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



        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (rdt != null && rdtCookie != 0)
            {
                try
                {
                    rdt.UnadviseRunningDocTableEvents(rdtCookie);
                    rdtCookie = 0;
                    Logger.LogDebug("DocumentSaveListener", "Unsubscribed from RDT events");
                }
                catch (Exception ex)
                {
                    Logger.LogError("DocumentSaveListener.Dispose", ex.ToString());
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
                "DocumentSaveListener",
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
                "DocumentSaveListener",
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
