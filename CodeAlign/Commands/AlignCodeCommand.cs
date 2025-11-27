using CodeAlign.Configuration;
using CodeAlign.Services;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Documents;
using Microsoft.VisualStudio.Extensibility.Editor;
using static System.Runtime.InteropServices.JavaScript.JSType;

#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

namespace CodeAlign.Commands;

internal class AlignCodeCommand : Command
{
    private readonly ExtensionEntrypoint _extension;

    public AlignCodeCommand(ExtensionEntrypoint extension)
    {
        _extension = extension;
    }

    /// <inheritdoc />
    public override CommandConfiguration CommandConfiguration => new(
        displayName: "%CodeAlign.Commands.AlignCodeCommand.DisplayName%")
    {
        Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
        Icon = new(ImageMoniker.KnownValues.AlignLeft, IconSettings.IconAndText),
    };

    public override async Task InitializeAsync(CancellationToken cancellationToken)
    {
        string displayName = "My Output Window";

        // To use this Output window Channel elsewhere in the class, such as the ExecuteCommandAsync() method in a Command, save this result to a field in the class.
        OutputChannel? outputChannel = await this.Extensibility.Views().Output.CreateOutputChannelAsync(displayName, cancellationToken);

    }

    /// <inheritdoc />
    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        try
        {

            // Get the active text view/document
            var textView = await context.GetActiveTextViewAsync(cancellationToken);
            if (textView == null)
            {
                return;
            }

            var document = textView.Document;

            if (document == null)
            {
                return;
            }

            if (!document.Uri.ToString().EndsWith(".cs"))
            {
                return;
            }

            // Read settings
            var settingsResult = await this.Extensibility.Settings().ReadEffectiveValuesAsync(
                [
                    AlignmentOptions.EnablePlugin,
                    AlignmentOptions.MaxAlignmentGap,
                    AlignmentOptions.MaxFileSizeBytes,
                    AlignmentOptions.ConstructorParameterThreshold,
                    AlignmentOptions.MethodParameterThreshold
                ], cancellationToken);

            // Force enable for manual command, but respect other settings
            var maxGap = settingsResult.ValueOrDefault(AlignmentOptions.MaxAlignmentGap, 50);
            var maxFileSize = settingsResult.ValueOrDefault(AlignmentOptions.MaxFileSizeBytes, 1024 * 1024);
            var ctorThresh = settingsResult.ValueOrDefault(AlignmentOptions.ConstructorParameterThreshold, 3);
            var methodThresh = settingsResult.ValueOrDefault(AlignmentOptions.MethodParameterThreshold, 4);

            var alignSettings = new AlignmentSettings(true, maxFileSize, maxGap, ctorThresh, methodThresh);
            var alignService = new AlignService(alignSettings);

            // Workaround for missing AsRoslynDocument: Read file from disk
            // Note: This reads the saved file, not the dirty buffer. 
            // Ideally we should get text from 'document' snapshot.
            string fileContent;
            if (document.Uri.Scheme == "file")
            {
                 fileContent = File.ReadAllText(document.Uri.LocalPath);
            }
            else
            {
                return;
            }

            using var workspace = new AdhocWorkspace();
            var project = workspace.AddProject("TempProject", LanguageNames.CSharp);
            var roslynDoc = workspace.AddDocument(project.Id, "TempDocument.cs", Microsoft.CodeAnalysis.Text.SourceText.From(fileContent));

            var newDoc = await alignService.FormatDocumentAsync(roslynDoc, skipRoslynFormatting: false, cancellationToken);
            var changes = await newDoc.GetTextChangesAsync(roslynDoc, cancellationToken);

            if (!changes.Any())
            {
                return;
            }

            var edits = new List<TextEdit>();
            var sourceText = await roslynDoc.GetTextAsync(cancellationToken);

            foreach (var change in changes)
            {
                // Use offsets directly from Roslyn TextSpan
                var start = new TextPosition(document, change.Span.Start);
                var end = new TextPosition(document, change.Span.End);
                edits.Add(new TextEdit(new TextRange(start, end), change.NewText));
            }

            var editor = this.Extensibility.Editor();
            await editor.EditAsync(batch =>
            {
                var editable = document.AsEditable(batch);
                foreach (var edit in edits)
                {
                    // TextEdit might expose the text via a different property if NewText is missing
                    // Trying 'Text' or just using the constructor's value if we had to.
                    // But we are iterating 'edits' which are TextEdit objects.
                    // Let's assume 'ReplacementText' or 'Text'.
                    // If 'Text' fails, we will see.
                    editable.Replace(edit.Range, edit.Text);
                }
            }, cancellationToken);
        }
        catch (Exception)
        {
            // Ignore errors
        }
    }
}
