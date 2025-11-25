using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;

namespace CodeFormatter
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
        public AlignService()
            : this(AlignmentSettings.Default)
        {
        }

        internal AlignService(AlignmentSettings settings)
            : this(settings, CreateDefaultProcessors(settings))
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

        /// <summary>
        /// Formats the given code with Roslyn default formatting + custom alignment
        /// </summary>
        /// <param name="code">Source code to format</param>
        /// <param name="skipRoslynFormatting">If true, skip Roslyn formatting and only apply alignment (useful when VS already formatted).
        /// If false, apply both Roslyn formatting and custom alignment in one pass.</param>
        /// <param name="workspace">Optional workspace for proper formatting. If null, uses AdhocWorkspace.</param>
        public string FormatCode(string code, bool skipRoslynFormatting = false, Workspace workspace = null)
        {
            if (string.IsNullOrEmpty(code))
                return code;
            try
            {
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetRoot();

                SyntaxNode formattedRoot = root;

                if (!skipRoslynFormatting)
                {
                    // Apply Roslyn default formatting using provided workspace or create a temporary one
                    Logger.LogDebug("AlignService", "Applying Roslyn IDE formatting + custom alignment in single pass");

                    var workspaceToUse = workspace ?? new AdhocWorkspace();
                    try
                    {
                        formattedRoot = Formatter.Format(root, workspaceToUse);
                    }
                    finally
                    {
                        // Dispose temp workspace if we created one
                        if (workspace == null)
                        {
                            workspaceToUse?.Dispose();
                        }
                    }
                }
                else
                {
                    Logger.LogDebug("AlignService", "Applying custom alignment only (skipping Roslyn formatting)");
                }

                // Apply custom alignment
                var alignedRoot = ApplyAlignmentProcessors(formattedRoot);
                var result = alignedRoot.ToFullString();

                if (result == code)
                {
                    Logger.LogDebug("AlignService", "Result identical to input - returning original to avoid artifacts");
                    return code;
                }

                Logger.LogDebug("AlignService", $"Content changed (delta: {result.Length - code.Length} chars)");
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError("AlignService.FormatCode", ex.ToString());
                return code;
            }
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

        /// <summary>
        /// Formats the given document with Roslyn default formatting + custom alignment.
        /// This method works with Document objects and returns the formatted Document,
        /// allowing the caller to compute minimal text changes.
        /// </summary>
        /// <param name="document">The document to format</param>
        /// <param name="skipRoslynFormatting">If true, skip Roslyn formatting and only apply alignment.
        /// If false, apply both Roslyn formatting and custom alignment.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The formatted document, or the original document if no changes were made</returns>
        public async Task<Document> FormatDocumentAsync(
            Document document, 
            bool skipRoslynFormatting = false, 
            CancellationToken cancellationToken = default)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            try
            {
                Document workingDocument = document;

                if (!skipRoslynFormatting)
                {
                    Logger.LogDebug("AlignService", "Applying Roslyn IDE formatting");
                    workingDocument = await Formatter.FormatAsync(workingDocument, cancellationToken: cancellationToken);
                }
                else
                {
                    Logger.LogDebug("AlignService", "Skipping Roslyn formatting (applying alignment only)");
                }

                // Get the syntax root
                var root = await workingDocument.GetSyntaxRootAsync(cancellationToken);
                if (root == null)
                {
                    Logger.LogDebug("AlignService", "Could not get syntax root");
                    return document;
                }

                // Apply custom alignment processors
                var alignedRoot = ApplyAlignmentProcessors(root);

                // If no changes, return original document
                if (alignedRoot == root)
                {
                    Logger.LogDebug("AlignService", "No alignment changes needed");
                    return workingDocument;
                }

                Logger.LogDebug("AlignService", "Alignment applied successfully");
                return workingDocument.WithSyntaxRoot(alignedRoot);
            }
            catch (Exception ex)
            {
                Logger.LogError("AlignService.FormatDocumentAsync", ex.ToString());
                return document;
            }
        }
    }
}
