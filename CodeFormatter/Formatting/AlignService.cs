using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;

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
        /// <param name="skipRoslynFormatting">If true, skip Roslyn formatting and only apply alignment (useful when VS already formatted)</param>
        public string FormatCode(string code, bool skipRoslynFormatting = false)
        {
            if (string.IsNullOrEmpty(code))
                return code;
            try
            {  
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetRoot();
                
                // IMPORTANT: We should NEVER apply Roslyn formatting in a VS extension
                // because we cannot replicate the user's .editorconfig and VS settings.
                // The IDE's formatter is much more sophisticated and respects user preferences.
                // Our job is ONLY to apply custom alignment.
                SyntaxNode formattedRoot = root;
                
                if (!skipRoslynFormatting)
                {
                    // Log a warning if someone tries to use built-in formatting
                    Logger.LogDebug("AlignService", "WARNING: skipRoslynFormatting=false is deprecated. Use IDE formatting instead.");
                }
                
                Logger.LogDebug("AlignService", "Applying custom alignment only (skipping Roslyn formatting)");
                
                // Apply custom alignment
                var alignedRoot = ApplyAlignmentProcessors(formattedRoot);
                var result = alignedRoot.ToFullString();
                
                // CRITICAL: Compare result with original to avoid round-trip artifacts
                if (result == code)
                {
                    Logger.LogDebug("AlignService", "Result identical to input - returning original to avoid artifacts");
                    return code;
                }
                
                Logger.LogDebug("AlignService", $"Alignment changed content (delta: {result.Length - code.Length} chars)");
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
    }
}
