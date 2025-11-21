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
    /// Listens to document save events and applies alignment formatting
    /// </summary>
    internal sealed class DocumentSaveListener : IVsRunningDocTableEvents3, IDisposable
    {
        private readonly IWpfTextView textView;
        private readonly SVsServiceProvider serviceProvider;
        private readonly AlignService alignService;
        private uint rdtCookie;
        private IVsRunningDocumentTable rdt;

        private DocumentSaveListener(IWpfTextView textView, SVsServiceProvider serviceProvider)
        {
            this.textView = textView;
            this.serviceProvider = serviceProvider;
            this.alignService = new AlignService();
        }

        public static DocumentSaveListener Create(
            IWpfTextView textView,
            SVsServiceProvider serviceProvider
        )
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            System.Diagnostics.Debug.WriteLine("[CodeFormatter] Creating DocumentSaveListener");
            var listener = new DocumentSaveListener(textView, serviceProvider);
            listener.Initialize();
            return listener;
        }

        private void Initialize()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                System.Diagnostics.Debug.WriteLine("[CodeFormatter] Initializing DocumentSaveListener");
                rdt = serviceProvider.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
                if (rdt != null)
                {
                    int hr = rdt.AdviseRunningDocTableEvents(this, out rdtCookie);
                    if (hr != VSConstants.S_OK)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CodeFormatter] Failed to advise running doc table events. HRESULT: {hr}");
                        ActivityLog.LogError("CodeFormatter.DocumentSaveListener", $"Failed to advise running doc table events. HRESULT: {hr}");
                        rdtCookie = 0;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[CodeFormatter] Successfully advised running doc table events. Cookie: {rdtCookie}");
                        ActivityLog.LogInformation("CodeFormatter.DocumentSaveListener", "Document save listener initialized successfully");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[CodeFormatter] Running document table service is null");
                    ActivityLog.LogError("CodeFormatter.DocumentSaveListener", "Could not get running document table service");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CodeFormatter] ERROR initializing DocumentSaveListener: {ex}");
                ActivityLog.LogError("CodeFormatter.DocumentSaveListener", $"Error initializing DocumentSaveListener: {ex}");
            }
        }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (rdt != null && rdtCookie != 0)
            {
                try
                {
                    int hr = rdt.UnadviseRunningDocTableEvents(rdtCookie);
                    if (hr != VSConstants.S_OK)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to unadvise running doc table events. HRESULT: {hr}");
                    }
                    rdtCookie = 0;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing DocumentSaveListener: {ex}");
                }
            }
        }

        #region IVsRunningDocTableEvents3 Implementation

        public int OnBeforeSave(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                // Get the document info
                uint grfRDTFlags;
                uint dwReadLocks;
                uint dwEditLocks;
                string pbstrMkDocument;
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
                        out pbstrMkDocument,
                        out ppHier,
                        out pitemid,
                        out ppunkDocData
                    );

                    if (hr != VSConstants.S_OK || string.IsNullOrEmpty(pbstrMkDocument))
                    {
                        return VSConstants.S_OK;
                    }

                    // Check if this is a C# file
                    if (!pbstrMkDocument.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        return VSConstants.S_OK;
                    }

                    System.Diagnostics.Debug.WriteLine($"[CodeFormatter] OnBeforeSave - C# file: {pbstrMkDocument}");

                    // Check if this is the document for our text view
                    var textBuffer = textView.TextBuffer;
                    if (textBuffer == null)
                    {
                        System.Diagnostics.Debug.WriteLine("[CodeFormatter] OnBeforeSave - TextBuffer is null");
                        return VSConstants.S_OK;
                    }

                    // Get the file path for the current text view
                    ITextDocument textDocument;
                    if (textBuffer.Properties.TryGetProperty(typeof(ITextDocument), out textDocument))
                    {
                        if (textDocument.FilePath != pbstrMkDocument)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CodeFormatter] OnBeforeSave - Document path mismatch");
                            return VSConstants.S_OK;
                        }

                        System.Diagnostics.Debug.WriteLine("[CodeFormatter] OnBeforeSave - Applying alignment");
                        ActivityLog.LogInformation("CodeFormatter.DocumentSaveListener", $"Applying alignment on save for: {pbstrMkDocument}");

                        // Apply alignment if enabled
                        AlignmentHelper.ApplyAlignment(textView, serviceProvider, alignService, checkFormatOnSave: true);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[CodeFormatter] OnBeforeSave - Could not get ITextDocument from buffer properties");
                    }
                }
                finally
                {
                    // Release the COM object to prevent leaks
                    if (ppunkDocData != IntPtr.Zero)
                    {
                        System.Runtime.InteropServices.Marshal.Release(ppunkDocData);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't prevent save
                ActivityLog.LogError("CodeFormatter.DocumentSaveListener", $"Error in OnBeforeSave: {ex}");
                System.Diagnostics.Debug.WriteLine($"[CodeFormatter] ERROR in OnBeforeSave: {ex}");
            }

            return VSConstants.S_OK;
        }

        #endregion

        #region Unused IVsRunningDocTableEvents3 methods

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie)
        {
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

        public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
        {
            return VSConstants.S_OK;
        }

        #endregion
    }
}
