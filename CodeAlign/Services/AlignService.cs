using CodeAlign.Configuration;
using CodeAlign.Processors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;

namespace CodeAlign.Services
{
    /// <summary>
    /// Service for aligning code
    /// </summary>
    public class AlignService
    {
        private readonly AlignmentSettings settings;
        private readonly IReadOnlyList<IAlignmentProcessor> processors;

        /// <summary>
        /// Initializes a new instance of AlignService with default settings
        /// </summary>
        public AlignService() : this(AlignmentSettings.Default)
        {
        }

        internal AlignService(AlignmentSettings settings) : this(settings, CreateDefaultProcessors(settings))
        {
        }

        internal AlignService(AlignmentSettings settings, IEnumerable<IAlignmentProcessor> processors)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (processors == null)
                throw new ArgumentNullException(nameof(processors));

            this.processors = processors as IReadOnlyList<IAlignmentProcessor> ?? processors.ToList();
        }

        private static IReadOnlyList<IAlignmentProcessor> CreateDefaultProcessors(AlignmentSettings settings)
        {
            return
            [
                new ParameterAlignmentProcessor(settings.ConstructorParameterThreshold, settings.MethodParameterThreshold),
                new ArgumentAlignmentProcessor(settings.MethodParameterThreshold),
                new AssignmentAlignmentProcessor(settings.MaxAlignmentGap),
                new ObjectInitializerAlignmentProcessor(settings.MaxAlignmentGap),
                new ChainedMethodAlignmentProcessor()
            ];
        }

        public async Task<Document> FormatDocumentAsync(
            Document document,
            bool skipRoslynFormatting = true,
            CancellationToken cancellationToken = default)
        {
            if (!skipRoslynFormatting)
            {
                document = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);
            }

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root == null) return document;

            var alignedRoot = ApplyAlignmentProcessors(root);

            if (alignedRoot == root)
                return document;

            return document.WithSyntaxRoot(alignedRoot);
        }

        private SyntaxNode ApplyAlignmentProcessors(SyntaxNode root)
        {
            var currentRoot = root;
            foreach (var processor in processors)
            {
                var nextRoot = processor.Apply(currentRoot);
                currentRoot = nextRoot;
            }
            return currentRoot;
        }
    }
}
