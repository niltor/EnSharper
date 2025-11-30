using CodeAlign.Configuration;
using CodeAlign.Services;
using EnvDTE;
using EnvDTE80;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Documents;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Extensibility.VSSdkCompatibility;
using Microsoft.VisualStudio.LanguageServices;
using Command = Microsoft.VisualStudio.Extensibility.Commands.Command;

#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

namespace CodeAlign.Commands;

//[VisualStudioContribution]
internal class FormatCommand : Command
{
    private OutputChannel OutputChannel { get; set; } = default!;
    private readonly AsyncServiceProviderInjection<DTE, DTE2> _dteInjection;
    private readonly AsyncServiceProviderInjection<SComponentModel, IComponentModel> _componentModelInjection;

    public FormatCommand(
        VisualStudioExtensibility extensibility,
        AsyncServiceProviderInjection<DTE, DTE2> dteInjection,
        AsyncServiceProviderInjection<SComponentModel, IComponentModel> componentModelInjection
        ) : base(extensibility)
    {
        _componentModelInjection = componentModelInjection;
        _dteInjection = dteInjection;
    }

    public override CommandConfiguration CommandConfiguration => new("FormatCommand")
    {
        VisibleWhen = ActivationConstraint.ClientContext(ClientContextKey.Shell.ActiveSelectionFileName, @"\.(cs)$"),
        EnabledWhen = ActivationConstraint.ClientContext(ClientContextKey.Shell.ActiveSelectionFileName, @"\.(cs)$"),
        Shortcuts =
        [
            new (ModifierKey.ShiftLeftAlt,Key.F)
        ],
        VsctCommandMapping = new VsctId(new Guid(VSConstants.CMDSETID.StandardCommandSet2K_string), (((uint)VSConstants.VSStd2KCmdID.FORMATDOCUMENT)))
    };


    public override async Task InitializeAsync(CancellationToken cancellationToken)
    {
        string displayName = "CodeAlign";
        // To use this Output window Channel elsewhere in the class, such as the ExecuteCommandAsync() method in a Command, save this result to a field in the class.
        OutputChannel = await Extensibility.Views().Output.CreateOutputChannelAsync(displayName, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        try
        {
            await OutputChannel.LogInfoAsync("Executing FormatCommand...");
            // Read settings
            var settingsResult = await Extensibility.Settings().ReadEffectiveValuesAsync(
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
                await OutputChannel.LogInfoAsync("Skip for disabled plugin");
                return;
            }

            var componentModel = await _componentModelInjection.GetServiceAsync();
            var workspace = componentModel.GetService<VisualStudioWorkspace>();
            if (workspace == null) return;

            var textView = await context.GetActiveTextViewAsync(cancellationToken);
            if (textView == null)
            {
                await OutputChannel.LogInfoAsync("No active text view found.");
                return;
            }
            var filePath = textView.Document.Uri.LocalPath;

            var documentId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(filePath)
                .FirstOrDefault();
            if (documentId == null) return;
            var document = workspace.CurrentSolution.GetDocument(documentId);
            if (document == null)
            {
                await OutputChannel.LogInfoAsync("No document found for the active text view.");
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
                return;
            }

            var edits = new List<TextEdit>();

            await Extensibility.Editor().EditAsync(batch =>
            {
                var editable = textView.Document.AsEditable(batch);

                foreach (var change in changes)
                {
                    var start = new TextPosition(textView.Document, change.Span.Start);
                    var end = new TextPosition(textView.Document, change.Span.End);
                    edits.Add(new TextEdit(new Microsoft.VisualStudio.Extensibility.Editor.TextRange(start, end), change.NewText));
                }

                foreach (var edit in edits)
                {
                    editable.Replace(edit.Range, edit.Text);
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            await OutputChannel.LogInfoAsync(ex.ToString());
        }
    }
}
