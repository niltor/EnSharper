using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace CodeFormatter
{
    /// <summary>
    /// Aligns invocation arguments when the count exceeds the configured threshold.
    /// </summary>
    internal class ArgumentAlignmentProcessor : IAlignmentProcessor
    {
        private readonly int argumentThreshold;

        public ArgumentAlignmentProcessor(int argumentThreshold)
        {
            this.argumentThreshold = argumentThreshold;
        }

        public SyntaxNode Apply(SyntaxNode root)
        {
            var rewriter = new ArgumentAlignmentRewriter(argumentThreshold);
            return rewriter.Visit(root);
        }

        private class ArgumentAlignmentRewriter : CSharpSyntaxRewriter
        {
            private readonly int argumentThreshold;

            public ArgumentAlignmentRewriter(int argumentThreshold)
            {
                this.argumentThreshold = argumentThreshold;
            }

            public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                if (node.ArgumentList != null && node.ArgumentList.Arguments.Count >= argumentThreshold)
                {
                    var indentation = DetectIndentation(node);
                    var newArgumentList = FormatArgumentList(node.ArgumentList, indentation);
                    node = node.WithArgumentList(newArgumentList);
                }

                return base.VisitInvocationExpression(node);
            }

            private string DetectIndentation(SyntaxNode node)
            {
                var current = node;
                while (current != null)
                {
                    var leadingTrivia = current.GetLeadingTrivia();
                    foreach (var trivia in leadingTrivia.Reverse())
                    {
                        if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                        {
                            var text = trivia.ToFullString();
                            return text + "    ";
                        }
                    }

                    current = current.Parent;
                }

                return "        ";
            }

            private ArgumentListSyntax FormatArgumentList(ArgumentListSyntax argumentList, string indentation)
            {
                if (argumentList.Arguments.Count == 0)
                {
                    return argumentList;
                }

                bool alreadyFormatted = true;
                int? previousLine = null;
                foreach (var arg in argumentList.Arguments)
                {
                    var lineSpan = arg.GetLocation().GetLineSpan();
                    int startLine = lineSpan.StartLinePosition.Line;
                    if (previousLine != null && startLine == previousLine)
                    {
                        alreadyFormatted = false;
                        break;
                    }

                    previousLine = startLine;
                }

                if (alreadyFormatted)
                {
                    return argumentList;
                }

                var newArguments = SyntaxFactory.SeparatedList<ArgumentSyntax>();
                foreach (var argument in argumentList.Arguments)
                {
                    var leadingTrivia = SyntaxFactory.TriviaList(
                        SyntaxFactory.CarriageReturnLineFeed,
                        SyntaxFactory.Whitespace(indentation)
                    );

                    var updatedArgument = argument.WithLeadingTrivia(leadingTrivia)
                        .WithTrailingTrivia(SyntaxFactory.TriviaList());
                    newArguments = newArguments.Add(updatedArgument);
                }

                var baseIndentation = indentation.Length >= 4
                    ? indentation.Substring(0, indentation.Length - 4)
                    : string.Empty;

                var closeParen = argumentList.CloseParenToken.WithLeadingTrivia(
                    SyntaxFactory.TriviaList(
                        SyntaxFactory.CarriageReturnLineFeed,
                        SyntaxFactory.Whitespace(baseIndentation)
                    )
                ).WithTrailingTrivia(SyntaxFactory.TriviaList());

                return SyntaxFactory.ArgumentList(
                    argumentList.OpenParenToken.WithTrailingTrivia(SyntaxFactory.TriviaList()),
                    newArguments,
                    closeParen
                );
            }
        }
    }
}
