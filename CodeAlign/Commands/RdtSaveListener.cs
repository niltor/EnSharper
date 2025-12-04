using CodeAlign.Configuration;
using CodeAlign.Services;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Documents;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
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

        var rdt = componentModel.GetService<SVsRunningDocumentTable>() as IVsRunningDocumentTable;
        rdt.GetDocumentInfo(
            docCookie,
            out uint rdtLocks,
            out uint pdwReadLocks,
            out uint itemId,
            out string documentPath,
            out IVsHierarchy ppHier,
            out uint pitemid,
            out IntPtr ppunkDocData
        );


        _ = HandleSaveAsync(documentPath);

        return VSConstants.S_OK;
    }

    private async Task HandleSaveAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await output.WriteLineAsync("[OnBeforeSave] 文档即将保存，被拦截!");
        var settingsResult = await extensibility.Settings().ReadEffectiveValuesAsync(
                [
                    AlignmentOptions.EnablePlugin,
                    AlignmentOptions.MaxAlignmentGap,
                    AlignmentOptions.MaxFileSizeBytes,
                    AlignmentOptions.ConstructorParameterThreshold,
                    AlignmentOptions.MethodParameterThreshold
                ], cancellationToken);

        var enable = settingsResult.ValueOrDefault(AlignmentOptions.EnablePlugin, true);
        if (!enable)
        {
            await output.LogInfoAsync("Skip for disabled plugin");
            return;
        }

        var workspace = componentModel.GetService<VisualStudioWorkspace>();
        if (workspace == null) return;

        var documentId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(filePath)
            .FirstOrDefault();
        if (documentId == null) return;
        var document = workspace.CurrentSolution.GetDocument(documentId);
        if (document == null)
        {
            await output.LogInfoAsync("No document found for the active text view.");
            return;
        }

        var maxGap = settingsResult.ValueOrDefault(AlignmentOptions.MaxAlignmentGap, 50);
        var maxFileSize = settingsResult.ValueOrDefault(AlignmentOptions.MaxFileSizeBytes, 1024 * 1024);
        var ctorThresh = settingsResult.ValueOrDefault(AlignmentOptions.ConstructorParameterThreshold, 3);
        var methodThresh = settingsResult.ValueOrDefault(AlignmentOptions.MethodParameterThreshold, 4);

        var alignSettings = new AlignmentSettings(enable, maxFileSize, maxGap, ctorThresh, methodThresh);
        var alignService = new AlignService(alignSettings);

        var formattedDocument = await alignService.FormatDocumentAsync(document, false, cancellationToken);
        var changes = await formattedDocument.GetTextChangesAsync(document, cancellationToken);

        if (!changes.Any())
        {
            await output.LogInfoAsync("codes has no changes");
            return;
        }

        var textDocumentFactory = componentModel.GetService<ITextDocumentFactoryService>();
        var textManager = componentModel.GetService<SVsTextManager>() as IVsTextManager;
        textManager.GetActiveView(1, null, out IVsTextView vsTextView);


        //foreach (var textView in textDocumentFactory.text)
        //{
        //    if (textView.TextBuffer is ITextBuffer buffer &&
        //        textDocumentFactory.TryGetTextDocument(buffer, out var textDocument) &&
        //        string.Equals(textDocument.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
        //    {
        //        // 找到了目标textView
        //        ApplyTextChangesToBuffer(buffer, changes);
        //        break;
        //    }
        //}

        await output.LogInfoAsync("Formatted code saved!");
    }
    void ApplyTextChangesToBuffer(ITextBuffer buffer, IEnumerable<TextChange> changes)
    {
        using (var edit = buffer.CreateEdit())
        {
            foreach (var change in changes.OrderByDescending(c => c.Span.Start)) // 倒序防偏移错误
            {
                edit.Replace(change.Span.Start, change.Span.Length, change.NewText);
            }
            edit.Apply();
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