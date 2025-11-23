using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using System;
using System.Collections.Generic;

namespace CodeFormatter
{
    /// <summary>
    /// Service for aligning code
    /// </summary>
    public class AlignService
    {
        // Default maximum file size to process (1MB) to prevent performance issues
        private const int DefaultMaxFileSizeBytes = 1024 * 1024;

        private readonly int maxFileSizeBytes;
        private readonly int maxAlignmentGap;
        private readonly int constructorParameterThreshold;
        private readonly int methodParameterThreshold;
        private readonly IReadOnlyList<IAlignmentProcessor> processors;

        /// <summary>
        /// Initializes a new instance of AlignService with default settings
        /// </summary>
        public AlignService()
            : this(DefaultMaxFileSizeBytes, 50, 3, 4)
        {
        }

        /// <summary>
        /// Initializes a new instance of AlignService with custom settings
        /// </summary>
        /// <param name="maxFileSizeBytes">Maximum file size to process</param>
        /// <param name="maxAlignmentGap">Maximum alignment gap in spaces (0 for unlimited)</param>
        /// <param name="constructorParameterThreshold">Constructor parameter threshold</param>
        /// <param name="methodParameterThreshold">Method parameter threshold</param>
        public AlignService(int maxFileSizeBytes, int maxAlignmentGap, int constructorParameterThreshold, int methodParameterThreshold)
        {
            this.maxFileSizeBytes = maxFileSizeBytes > 0 ? maxFileSizeBytes : DefaultMaxFileSizeBytes;
            this.maxAlignmentGap = maxAlignmentGap >= 0 ? maxAlignmentGap : 0; // 0 means unlimited
            this.constructorParameterThreshold = Math.Max(2, constructorParameterThreshold);
            this.methodParameterThreshold = Math.Max(2, methodParameterThreshold);
            processors = new IAlignmentProcessor[]
            {
                new ParameterAlignmentProcessor(this.constructorParameterThreshold, this.methodParameterThreshold),
                new ArgumentAlignmentProcessor(this.methodParameterThreshold),
                new AssignmentAlignmentProcessor(this.maxAlignmentGap)
            };
        }

        /// <summary>
        /// Formats the given code with Roslyn default formatting + custom alignment
        /// </summary>
        public string FormatCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return code;

            try
            {
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetRoot();

                // 1. Apply Roslyn default formatting first
                var workspace = new AdhocWorkspace();
                var formattedRoot = Formatter.Format(root, workspace);
                var formattedText = formattedRoot.ToFullString();

                // 2. Apply custom alignment transformations on the formatted tree
                var alignedRoot = ApplyAlignmentProcessors(formattedRoot, out bool alignmentChanged);
                var result = alignmentChanged ? alignedRoot.ToFullString() : formattedText;

                // Ensure idempotency - if result is same as input, return input to avoid unnecessary updates
                if (result == code)
                    return code;

                return result;
            }
            catch (Exception ex)
            {
                // If parsing fails, log the exception and return original code
                System.Diagnostics.Debug.WriteLine($"[AlignService.FormatCode] Exception: {ex.Message}");
                return code;
            }
        }

        /// <summary>
        /// Applies only the alignment processors without re-formatting the document.
        /// </summary>
        public string ApplyAlignmentOnly(string code)
        {
            if (string.IsNullOrEmpty(code))
                return code;

            try
            {
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetRoot();
                var alignedRoot = ApplyAlignmentProcessors(root, out _);
                return alignedRoot.ToFullString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlignService.ApplyAlignmentOnly] Exception: {ex.Message}");
                return code;
            }
        }

        private SyntaxNode ApplyAlignmentProcessors(SyntaxNode root, out bool changed)
        {
            changed = false;
            var currentRoot = root;

            foreach (var processor in processors)
            {
                var beforeText = currentRoot.ToFullString();
                var nextRoot = processor.Apply(currentRoot);
                var afterText = nextRoot.ToFullString();

                if (!changed && !string.Equals(beforeText, afterText, StringComparison.Ordinal))
                {
                    changed = true;
                }

                currentRoot = nextRoot;
            }

            return currentRoot;
        }

    }
}
