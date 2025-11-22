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
        private bool isFormatting = false;
        private string lastFormattedText = null;

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

            var listener = new DocumentSaveListener(textView, serviceProvider);
            listener.Initialize();
            return listener;
        }

        private void Initialize()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                // GetService can return null; check before casting
                var svc = serviceProvider.GetService(typeof(SVsRunningDocumentTable));
                if (svc == null)
                {
                    System.Diagnostics.Debug.WriteLine("SVsRunningDocumentTable service not available.");
                    return;
                }

                rdt = svc as IVsRunningDocumentTable;
                if (rdt != null)
                {
                    int hr = rdt.AdviseRunningDocTableEvents(this, out rdtCookie);
                    if (hr != VSConstants.S_OK)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to advise running doc table events. HRESULT: {hr}");
                        rdtCookie = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing DocumentSaveListener: {ex}");
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

            // Prevent re-entrant formatting
            if (isFormatting)
            {
                System.Diagnostics.Debug.WriteLine("DocumentSaveListener: Skipping format - already formatting");
                return VSConstants.S_OK;
            }

            try
            {
                isFormatting = true;

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
                        return VSConstants.S_OK;

                    // Check if this is a C# file
                    if (!pbstrMkDocument.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                        return VSConstants.S_OK;

                    // Check if this is the document for our text view
                    var textBuffer = textView.TextBuffer;
                    if (textBuffer == null)
                        return VSConstants.S_OK;

                    // Get the file path for the current text view
                    ITextDocument textDocument;
                    if (textBuffer.Properties.TryGetProperty(typeof(ITextDocument), out textDocument))
                    {
                        if (textDocument.FilePath != pbstrMkDocument)
                            return VSConstants.S_OK;

                        // Check if we already formatted this exact text to prevent duplicate formatting
                        var currentText = textBuffer.CurrentSnapshot.GetText();
                        if (lastFormattedText == currentText)
                        {
                            System.Diagnostics.Debug.WriteLine("DocumentSaveListener: Skipping format - text unchanged since last format");
                            return VSConstants.S_OK;
                        }

                        // Apply alignment if enabled
                        AlignmentHelper.ApplyAlignment(textView, serviceProvider, alignService, checkFormatOnSave: true);
                        
                        // Remember the formatted text
                        lastFormattedText = textBuffer.CurrentSnapshot.GetText();
                        
                        System.Diagnostics.Debug.WriteLine("DocumentSaveListener: Format applied on save");
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
                System.Diagnostics.Debug.WriteLine($"Error in OnBeforeSave: {ex}");
            }
            finally
            {
                isFormatting = false;
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
