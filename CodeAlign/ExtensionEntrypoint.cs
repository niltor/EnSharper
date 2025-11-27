// using CodeAlign.Listeners;
using CodeAlign.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Extensibility;

namespace CodeAlign
{
    /// <summary>
    /// Extension entrypoint for the VisualStudio.Extensibility extension.
    /// </summary>
    [VisualStudioContribution]
    internal class ExtensionEntrypoint : Extension
    {
        /// <inheritdoc />
        public override ExtensionConfiguration ExtensionConfiguration => new()
        {
            RequiresInProcessHosting = false,
            Metadata = new(
                id: "CodeAlign.66bb6a15-1595-4aef-82c2-1942274d70d9",
                version: this.ExtensionAssemblyVersion,
                publisherName: "NilTor",
                displayName: "CodeAlign",
                description: "VisualStudio.Extensibility Extension for Code Alignment.")
        };

        /*
        [VisualStudioContribution]
        public static OutputChannelConfiguration OutputChannel => new()
        {
            DisplayName = "CodeAlign",
        };
        */

        /// <inheritdoc />
        protected override void InitializeServices(IServiceCollection serviceCollection)
        {
            base.InitializeServices(serviceCollection);
            // serviceCollection.AddScoped<DocumentEventListener>();
            serviceCollection.AddScoped<AlignService>();
        }

        /*
        /// <inheritdoc />
        public override void Initialize(ExtensionActivationContext context)
        {
            base.Initialize(context);
            context.RegisterDocumentEventsListener(new DocumentEventListener(this));
        }
        */
    }
}
