using CodeFormatter.Configuration;
using CodeFormatter.Processors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CodeFormatter.Services
{
    /// <summary>
    /// Service for aligning code
    /// </summary>
    public class AlignService
    {
        private readonly AlignmentSettings settings;
        private readonly IReadOnlyList<IAlignmentProcessor> processors;

        internal bool IsEnabled => settings.IsEnabled;

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
            return new IAlignmentProcessor[]
            {
                new ParameterAlignmentProcessor(settings.ConstructorParameterThreshold, settings.MethodParameterThreshold),
                new ArgumentAlignmentProcessor(settings.MethodParameterThreshold),
                new AssignmentAlignmentProcessor(settings.MaxAlignmentGap),
                new ObjectInitializerAlignmentProcessor(settings.MaxAlignmentGap),
                new ChainedMethodAlignmentProcessor()
            };
        }

        public async Task<Document> FormatDocumentAsync(Document document, bool skipRoslynFormatting = true, CancellationToken cancellationToken = default)
        {
            if (!skipRoslynFormatting)
            {
                document = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);
            }

            var root = await document.GetSyntaxRootAsync(cancellationToken);
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

        public static Document GetActiveDocument(SVsServiceProvider serviceProvider, out IWpfTextView textView)
        {
            try
            {
                var txtMgr = serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager;
                txtMgr.GetActiveView(1, null, out IVsTextView vsTextView);

                // 通过 MEF 获取 ITextView
                var componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
                var adapterService = componentModel.GetService<IVsEditorAdaptersFactoryService>();
                textView = adapterService.GetWpfTextView(vsTextView);
                textView.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument textDocument);

                // 通过 MEF 获取 Roslyn Workspace
                Workspace workspace = componentModel.GetService<Workspace>();
                var documentId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(textDocument.FilePath)
                    .FirstOrDefault();

                var docment = workspace.CurrentSolution.GetDocument(documentId);
                if (docment.TryGetText(out SourceText sourceText))
                {
                    var activeDocumentId = workspace.GetDocumentIdInCurrentContext(sourceText.Container);
                    return workspace.CurrentSolution.GetDocument(activeDocumentId);
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError("DocumentFormatListener.GetActiveTextView", ex.ToString());
                textView = null;
                return null;
            }
        }
    }
}
