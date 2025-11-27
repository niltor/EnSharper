/*
using CodeAlign.Configuration;
using CodeAlign.Services;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Documents;
using Microsoft.VisualStudio.Extensibility.Editor;

namespace CodeAlign.Listeners
{
    internal class DocumentEventListener : IDocumentEventsListener
    {
        private readonly ExtensionEntrypoint _extension;

        public DocumentEventListener(ExtensionEntrypoint extension)
        {
            _extension = extension;
        }

        public async Task SavedAsync(DocumentEventArgs e, CancellationToken cancellationToken)
        {
            // ... implementation ...
        }

        public Task ClosedAsync(DocumentEventArgs e, CancellationToken token) => Task.CompletedTask;
        public Task HiddenAsync(DocumentEventArgs e, CancellationToken token) => Task.CompletedTask;
        public Task OpenedAsync(DocumentEventArgs e, CancellationToken token) => Task.CompletedTask;
        public Task RenamedAsync(RenamedDocumentEventArgs e, CancellationToken token) => Task.CompletedTask;
        public Task SavingAsync(DocumentEventArgs e, CancellationToken token) => Task.CompletedTask;
        public Task ShownAsync(DocumentEventArgs e, CancellationToken token) => Task.CompletedTask;
    }
}
*/