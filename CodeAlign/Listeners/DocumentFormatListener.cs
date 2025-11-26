using CodeAlign.Services;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Document = Microsoft.CodeAnalysis.Document;

namespace CodeAlign.Listeners
{
    /// <summary>
    /// Global singleton listener for Format Document keyboard shortcuts.
    /// </summary>
    internal sealed class DocumentFormatListener : IDisposable
    {
        private static DocumentFormatListener instance;
        private static readonly object lockObject = new object();

        private readonly SVsServiceProvider serviceProvider;
        private EnvDTE.CommandEvents commandEvents;
        private string lastFormattedContent = null;

        private DocumentFormatListener(SVsServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.serviceProvider = serviceProvider;
            TryAttachToFormatCommand();
        }

        public static DocumentFormatListener GetOrCreate(SVsServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            lock (lockObject)
            {
                if (instance == null)
                {
                    instance = new DocumentFormatListener(serviceProvider);
                    Logger.LogDebug("DocumentFormatListener", "Global instance created");
                }
                return instance;
            }
        }

        private void TryAttachToFormatCommand()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var dte = serviceProvider.GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                if (dte == null)
                {
                    Logger.LogDebug("DocumentFormatListener", "DTE not available");
                    return;
                }

                var cmd = dte.Commands.Item("Edit.FormatDocument");
                commandEvents = dte.Events.get_CommandEvents(cmd.Guid, cmd.ID);
                commandEvents.BeforeExecute += OnBeforeExecute;

                Logger.LogDebug(
                    "DocumentFormatListener",
                    "Attached to Edit.FormatDocument CommandEvents (BeforeExecute)"
                );
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    "DocumentFormatListener.TryAttachToFormatCommand",
                    ex.ToString()
                );
            }
        }

        private void OnBeforeExecute(string guid, int id, object customIn, object customOut, ref bool cancelDefault)
        {
            // Default to not cancelling the IDE formatting.
            cancelDefault = false;
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
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
                Logger.LogError("DocumentFormatListener.OnBeforeExecute", ex.ToString());
            }
        }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                if (commandEvents != null)
                {
                    commandEvents.BeforeExecute -= OnBeforeExecute;
                    commandEvents = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("DocumentFormatListener.Dispose", ex.ToString());
            }
        }
    }
}
