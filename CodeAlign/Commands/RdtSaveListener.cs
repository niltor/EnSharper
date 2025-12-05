using CodeAlign.Configuration;
using CodeAlign.Services;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Documents;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace CodeAlign.Commands;

#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
internal class RdtSaveListener(
    VisualStudioExtensibility extensibility,
    IComponentModel componentModel,
    OutputChannel output
    ) : IVsRunningDocTableEvents
{
    public int OnBeforeSave(uint docCookie)
    {
        ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
            await HandleSaveAsync(docCookie);
        });

        return VSConstants.S_OK;
    }

    private async Task HandleSaveAsync(uint docCookie, CancellationToken cancellationToken = default)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        // 1. Get the Buffer from the Cookie
        var rdt = componentModel.GetService<SVsRunningDocumentTable>() as IVsRunningDocumentTable;
        rdt.GetDocumentInfo(docCookie, out _, out _, out _, out string documentPath, out _, out _, out IntPtr ppunkDocData);

        if (ppunkDocData == IntPtr.Zero) return;
        if (Path.GetExtension(documentPath) != ".cs") return;
        try
        {
            // 2. Get ITextBuffer using Editor Adapters
            // We need IVsEditorAdaptersFactoryService to go from Legacy -> Modern API
            var editorAdapters = componentModel.GetService<IVsEditorAdaptersFactoryService>();
            var vsTextBuffer = System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(ppunkDocData) as IVsTextBuffer;

            if (vsTextBuffer == null) return;

            var textBuffer = editorAdapters.GetDataBuffer(vsTextBuffer);
            if (textBuffer == null) return;

            var settingsResult = await extensibility.Settings().ReadEffectiveValuesAsync(
                [
                    AlignmentOptions.EnablePlugin,
                    AlignmentOptions.MaxAlignmentGap,
                    AlignmentOptions.MaxFileSizeBytes,
                    AlignmentOptions.ConstructorParameterThreshold,
                    AlignmentOptions.MethodParameterThreshold
                ], cancellationToken);

            if (!settingsResult.ValueOrDefault(AlignmentOptions.EnablePlugin, true)) return;

            // 4. Get Roslyn Document
            var workspace = componentModel.GetService<VisualStudioWorkspace>();
            var documentId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(documentPath).FirstOrDefault();
            if (documentId == null) return;

            var document = workspace.CurrentSolution.GetDocument(documentId);
            if (document == null)
            {
                await output.LogInfoAsync($"Document not found in workspace: {documentPath}");
                return;
            }
            document = document.WithText(textBuffer.CurrentSnapshot.AsText());

            var maxGap = settingsResult.ValueOrDefault(AlignmentOptions.MaxAlignmentGap, 50);
            var maxFileSize = settingsResult.ValueOrDefault(AlignmentOptions.MaxFileSizeBytes, 1024 * 1024);
            var ctorThresh = settingsResult.ValueOrDefault(AlignmentOptions.ConstructorParameterThreshold, 3);
            var methodThresh = settingsResult.ValueOrDefault(AlignmentOptions.MethodParameterThreshold, 4);

            var alignSettings = new AlignmentSettings(maxFileSize, maxGap, ctorThresh, methodThresh);
            var alignService = new AlignService(alignSettings);

            var formattedDocument = await alignService.FormatDocumentAsync(document, false, cancellationToken);
            var changes = await formattedDocument.GetTextChangesAsync(document, cancellationToken);

            if (!changes.Any()) return;

            // 6. APPLY CHANGES (The core answer to your question)
            // We create an Edit transaction on the ITextBuffer
            using (var edit = textBuffer.CreateEdit())
            {
                foreach (var change in changes)
                {
                    edit.Replace(change.Span.Start, change.Span.Length, change.NewText);
                }
                // Apply creates a single undo unit for all changes
                edit.Apply();
            }

            await output.LogInfoAsync($"Applied {changes.Count()} formatting changes.");
        }
        finally
        {
            // Release COM pointer if strictly necessary, but usually handled by marshaling in simple cases
            if (ppunkDocData != IntPtr.Zero)
                System.Runtime.InteropServices.Marshal.Release(ppunkDocData);
        }
    }
    #region Implemented
    public int OnBeforeLastDocumentUnlock(
      uint docCookie,
      uint dwRDTLockType,
      uint dwReadLocksRemaining,
      uint dwEditLocksRemaining
  )
    {
        return VSConstants.S_OK;
    }

    public int OnAfterFirstDocumentLock(
        uint docCookie,
        uint dwRDTLockType,
        uint dwReadLocksRemaining,
        uint dwEditLocksRemaining
    )
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
    #endregion
}