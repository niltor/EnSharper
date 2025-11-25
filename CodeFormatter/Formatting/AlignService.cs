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
                new AssignmentAlignmentProcessor(settings.MaxAlignmentGap)
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

        /// <summary>
        /// Formats a Document with Roslyn default formatting + custom alignment using Document API for differential updates.
        /// This method is preferred when working with VS Workspace to avoid full text replacement and reduce flickering.
        /// </summary>
        /// <param name="document">The document to format</param>
        /// <param name="skipRoslynFormatting">If true, skip Roslyn formatting and only apply alignment</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A new Document with formatting applied, or the original if no changes needed</returns>
        public async Task<Document> FormatDocumentAsync(
            Document document,
            bool skipRoslynFormatting = false,
            CancellationToken cancellationToken = default)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            Document formattedDocument = document;
            try
            {
                // Apply Roslyn IDE formatting if requested
                if (!skipRoslynFormatting)
                {
                    Logger.LogDebug("AlignService", "Applying Roslyn IDE formatting to Document");
                    formattedDocument = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);
                }
                else
                {
                    Logger.LogDebug("AlignService", "Skipping Roslyn formatting (applying custom alignment only)");
                }

                // Get syntax root and apply custom alignment
                var root = await formattedDocument.GetSyntaxRootAsync(cancellationToken);
                if (root == null)
                {
                    Logger.LogDebug("AlignService", "Could not get syntax root from document");
                    return document;
                }

                var alignedRoot = ApplyAlignmentProcessors(root);

                // If alignment didn't change anything, return the formatted document as-is
                if (alignedRoot == root)
                {
                    Logger.LogDebug("AlignService", "No alignment changes needed");
                    return formattedDocument;
                }

                // Return document with aligned syntax root
                Logger.LogDebug("AlignService", "Alignment applied to document");
                return formattedDocument.WithSyntaxRoot(alignedRoot);
            }
            catch (Exception ex)
            {
                Logger.LogError("AlignService.FormatDocumentAsync", ex.ToString());
                // Return formattedDocument if Roslyn formatting succeeded, otherwise return original document
                return formattedDocument;
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
    }
}
