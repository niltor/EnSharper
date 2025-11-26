using Microsoft.CodeAnalysis;

namespace CodeAlign.Processors
{
    /// <summary>
    /// Applies a single alignment step to a syntax tree.
    /// </summary>
    internal interface IAlignmentProcessor
    {
        SyntaxNode Apply(SyntaxNode root);
    }
}
