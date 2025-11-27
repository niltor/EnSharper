using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAlign.Processors
{
    /// <summary>
    /// Aligns assignment operators in object initializers.
    /// </summary>
    internal class ObjectInitializerAlignmentProcessor : IAlignmentProcessor
    {
        private readonly int maxAlignmentGap;

        public ObjectInitializerAlignmentProcessor(int maxAlignmentGap)
        {
            this.maxAlignmentGap = maxAlignmentGap;
        }

        public SyntaxNode Apply(SyntaxNode root)
        {
            var rewriter = new ObjectInitializerAlignmentRewriter(maxAlignmentGap);
            return rewriter.Visit(root);
        }

        private class ObjectInitializerAlignmentRewriter : CSharpSyntaxRewriter
        {
            private readonly int maxAlignmentGap;

            public ObjectInitializerAlignmentRewriter(int maxAlignmentGap)
            {
                this.maxAlignmentGap = maxAlignmentGap;
            }

            public override SyntaxNode VisitInitializerExpression(InitializerExpressionSyntax node)
            {
                // Only process object initializers, not collection initializers
                if (node.IsKind(SyntaxKind.ObjectInitializerExpression))
                {
                    node = AlignObjectInitializer(node);
                }

                return base.VisitInitializerExpression(node);
            }

            private InitializerExpressionSyntax AlignObjectInitializer(InitializerExpressionSyntax initializer)
            {
                if (initializer.Expressions.Count <= 1)
                {
                    return initializer;
                }

                // Check if all expressions are on separate lines
                var expressions = initializer.Expressions.ToList();
                bool allOnSeparateLines = true;
                int? previousLine = null;

                foreach (var expr in expressions)
                {
                    var currentLine = expr.GetLocation().GetLineSpan().StartLinePosition.Line;
                    if (previousLine.HasValue && currentLine == previousLine.Value)
                    {
                        allOnSeparateLines = false;
                        break;
                    }
                    previousLine = currentLine;
                }

                if (!allOnSeparateLines)
                {
                    return initializer;
                }

                // Get property name positions (left side of =)
                var propertyPositions = new List<int>();
                foreach (var expr in expressions)
                {
                    if (expr is AssignmentExpressionSyntax assignment)
                    {
                        var leftText = assignment.Left.ToString().Trim();
                        propertyPositions.Add(leftText.Length);
                    }
                    else
                    {
                        // Not all expressions are assignments, skip alignment
                        return initializer;
                    }
                }

                if (propertyPositions.Count == 0)
                {
                    return initializer;
                }

                var maxPropertyPos = propertyPositions.Max();

                // Check if gap is within limit
                if (maxAlignmentGap > 0)
                {
                    var minPropertyPos = propertyPositions.Min();
                    if (maxPropertyPos - minPropertyPos > maxAlignmentGap)
                    {
                        // Property alignment gap exceeds maximum, skipping
                        return initializer;
                    }
                }

                // Align the assignments - use ReplaceNodes to preserve separators
                var alignedExpressions = new Dictionary<ExpressionSyntax, ExpressionSyntax>();

                for (int i = 0; i < expressions.Count; i++)
                {
                    if (expressions[i] is AssignmentExpressionSyntax assignment)
                    {
                        var propertyPos = propertyPositions[i];
                        var spacesToAdd = maxPropertyPos - propertyPos;

                        // Replace trailing trivia with new spacing for alignment
                        // This intentionally replaces any existing trivia to ensure consistent alignment
                        var newLeft = assignment.Left.WithTrailingTrivia(
                            SyntaxFactory.Whitespace(new string(' ', spacesToAdd + 1))
                        );
                        var newAssignment = assignment.WithLeft(newLeft);
                        alignedExpressions[assignment] = newAssignment;
                    }
                }

                if (alignedExpressions.Count == 0)
                {
                    return initializer;
                }

                // Replace the old expressions with aligned ones
                var newInitializer = initializer.ReplaceNodes(
                    alignedExpressions.Keys,
                    (oldNode, _) => alignedExpressions[oldNode]
                );

                return newInitializer;
            }
        }
    }
}
