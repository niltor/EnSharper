using CodeAlign.Configuration;
using CodeAlign.Services;
using EnvDTE;
using EnvDTE80;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Documents;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Extensibility.VSSdkCompatibility;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Command = Microsoft.VisualStudio.Extensibility.Commands.Command;

#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

namespace CodeAlign.Commands;

[VisualStudioContribution]
internal class SaveCommand : Command
{
    private OutputChannel OutputChannel { get; set; } = default!;
    private readonly AsyncServiceProviderInjection<DTE, DTE2> _dteInjection;
    private readonly AsyncServiceProviderInjection<SComponentModel, IComponentModel> _componentModelInjection;


    public SaveCommand(
        VisualStudioExtensibility extensibility,
        AsyncServiceProviderInjection<DTE, DTE2> dteInjection,
        AsyncServiceProviderInjection<SComponentModel, IComponentModel> componentModelInjection
        ) : base(extensibility)
    {
        _componentModelInjection = componentModelInjection;
        _dteInjection = dteInjection;
        DisableDuringExecution = true;
    }

    public override CommandConfiguration CommandConfiguration => new("%CodeAlign.Commands.SaveCommand.DisplayName%")
    {
        Placements =
        [
            CommandPlacement.KnownPlacements.ExtensionsMenu.WithPriority(1),
        ],
        VisibleWhen = ActivationConstraint.ClientContext(ClientContextKey.Shell.ActiveEditorFileName, @"\.(cs)$"),
        EnabledWhen = ActivationConstraint.ClientContext(ClientContextKey.Shell.ActiveEditorFileName, @"\.(cs)$"),
        Shortcuts =
        [
            new(ModifierKey.ControlShift, Key.J),
            //new(ModifierKey.Control, Key.S)

        ],
        //VsctCommandMapping = new VsctId(new Guid(VSConstants.CMDSETID.StandardCommandSet2K_string), (((uint)VSConstants.VSStd97CmdID.Save)))
    };

    public override async Task InitializeAsync(CancellationToken cancellationToken)
    {
        string displayName = "CodeAlign";
        OutputChannel = await Extensibility.Views().Output
            .CreateOutputChannelAsync(displayName, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        try
        {
            await OutputChannel.LogInfoAsync("Executing SaveCommand...");
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

            var textView = await context.GetActiveTextViewAsync(cancellationToken);
            if (textView == null)
            {
                await OutputChannel.LogInfoAsync("No active text view found.");
                return;
            }
            var filePath = textView.Document.Uri.LocalPath;
            if (!filePath.EndsWith(".cs"))
            {
                await OutputChannel.LogInfoAsync("Skip non-C# file");
                return;
            }

            var componentModel = await _componentModelInjection.GetServiceAsync();
            var workspace = componentModel.GetService<VisualStudioWorkspace>();
            if (workspace == null) return;

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

            var alignSettings = new AlignmentSettings(maxFileSize, maxGap, ctorThresh, methodThresh);
            var alignService = new AlignService(alignSettings);

            var formattedDocument = await alignService.FormatDocumentAsync(document, false, cancellationToken);
            var changes = await formattedDocument.GetTextChangesAsync(document, cancellationToken);

            if (!changes.Any())
            {
                await OutputChannel.LogInfoAsync("codes has no changes");
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

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            var dte = await _dteInjection.GetServiceAsync();
            dte.ActiveDocument?.Save();

            await OutputChannel.LogInfoAsync("Formatted code saved!");

        }
        catch (Exception ex)
        {
            await OutputChannel.LogInfoAsync(ex.ToString());
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
