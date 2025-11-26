using CodeAlign.Configuration;
using CodeAlign.Processors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace CodeAlign.Services
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
                var adapterService = componentModel?.GetService<IVsEditorAdaptersFactoryService>();
                textView = adapterService?.GetWpfTextView(vsTextView);
                if (textView == null)
                    return null;

                if (!textView.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument textDocument) || textDocument == null)
                {
                    Logger.LogError("DocumentFormatListener.GetActiveTextView", "Failed to get ITextDocument for the active view.");
                    return null;
                }

                // 首选从 TextBuffer 的 Properties 获取 Workspace（更可靠）
                Workspace workspace = null;
                if (!textView.TextBuffer.Properties.TryGetProperty(typeof(Workspace), out workspace) || workspace == null)
                {
                    // 回退到 ComponentModel 获取全局 Workspace，但此调用在某些环境下可能没有导出
                    try
                    {
                        workspace = componentModel?.GetService<Workspace>();
                    }
                    catch (Exception)
                    {
                        // 可能抛出 CompositionFailedException，当没有可用的 Workspace 导出时
                        workspace = null;
                    }
                }

                if (workspace == null)
                {
                    Logger.LogError("DocumentFormatListener.GetActiveTextView", "Failed to get Workspace for the active document.");
                    return null;
                }

                var documentId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(textDocument.FilePath)
                    .FirstOrDefault();

                if (documentId == null)
                {
                    Logger.LogError("DocumentFormatListener.GetActiveTextView", $"No DocumentId found for file path: {textDocument.FilePath}");
                    return null;
                }

                return workspace.CurrentSolution.GetDocument(documentId);
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
