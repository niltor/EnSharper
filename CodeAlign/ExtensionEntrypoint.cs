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
            RequiresInProcessHosting = true,
        };

        /*
        [VisualStudioContribution]
        public static OutputChannelConfiguration OutputChannel => new()
        {
            DisplayName = "CodeAlign",
        };
        */

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
