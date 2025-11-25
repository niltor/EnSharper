using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeFormatter
{
    /// <summary>
    /// Ensures chained method calls are on separate lines and properly aligned.
    /// </summary>
    internal class ChainedMethodAlignmentProcessor : IAlignmentProcessor
    {
        private readonly int minChainLength;
        private const int IndentationSpaces = 4; // Standard indentation increment

        public ChainedMethodAlignmentProcessor(int minChainLength = 2)
        {
            this.minChainLength = minChainLength;
        }

        public SyntaxNode Apply(SyntaxNode root)
        {
            var rewriter = new ChainedMethodAlignmentRewriter(minChainLength);
            return rewriter.Visit(root);
        }

        private class ChainedMethodAlignmentRewriter : CSharpSyntaxRewriter
        {
            private readonly int minChainLength;

            public ChainedMethodAlignmentRewriter(int minChainLength)
            {
                this.minChainLength = minChainLength;
            }

            public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                // Only process top-level invocations (not nested in a member access)
                if (node.Parent is MemberAccessExpressionSyntax)
                {
                    return base.VisitInvocationExpression(node);
                }

                // Only process chains inside method bodies, not at class field/property level
                if (!IsInsideMethodBody(node))
                {
                    return base.VisitInvocationExpression(node);
                }

                // Check if this invocation is part of a chain
                if (node.Expression is MemberAccessExpressionSyntax)
                {
                    var chainLength = CountChainDepth(node.Expression);
                    
                    if (chainLength >= minChainLength)
                    {
                        // Check if already on separate lines
                        if (!AreChainCallsOnSeparateLines(node.Expression))
                        {
                            // Format the chain
                            var indentation = DetectIndentation(node);
                            var formattedNode = FormatChainCalls(node, indentation);
                            return formattedNode;
                        }
                    }
                }

                return base.VisitInvocationExpression(node);
            }

            private bool IsInsideMethodBody(SyntaxNode node)
            {
                var current = node.Parent;
                while (current != null)
                {
                    // Check if we're inside a method, constructor, property accessor, etc.
                    if (current is MethodDeclarationSyntax ||
                        current is ConstructorDeclarationSyntax ||
                        current is AccessorDeclarationSyntax ||
                        current is LocalFunctionStatementSyntax ||
                        current is AnonymousFunctionExpressionSyntax)
                    {
                        return true;
                    }

                    // Stop if we hit a type declaration (class, struct, etc.)
                    if (current is TypeDeclarationSyntax)
                    {
                        return false;
                    }

                    current = current.Parent;
                }

                return false;
            }

            private int CountChainDepth(SyntaxNode node)
            {
                int count = 0;
                var current = node;

                while (current is MemberAccessExpressionSyntax memberAccess)
                {
                    count++;
                    current = memberAccess.Expression;
                }

                return count;
            }

            private bool AreChainCallsOnSeparateLines(SyntaxNode node)
            {
                int? previousLine = null;
                var current = node;

                while (current is MemberAccessExpressionSyntax memberAccess)
                {
                    var nameLineSpan = memberAccess.Name.GetLocation().GetLineSpan();
                    int currentLine = nameLineSpan.StartLinePosition.Line;
                    
                    if (previousLine != null && currentLine == previousLine)
                    {
                        return false;
                    }
                    
                    previousLine = currentLine;
                    current = memberAccess.Expression;
                }

                return true;
            }

            private string DetectIndentation(SyntaxNode node)
            {
                var current = node;
                while (current != null)
                {
                    var leadingTrivia = current.GetLeadingTrivia();
                    
                    // Iterate through trivia to find whitespace after the last newline
                    string lastWhitespace = "";
                    for (int i = 0; i < leadingTrivia.Count; i++)
                    {
                        var trivia = leadingTrivia[i];
                        if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                        {
                            // Reset - we found a newline, next whitespace is the indentation
                            lastWhitespace = "";
                        }
                        else if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                        {
                            lastWhitespace = trivia.ToFullString();
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(lastWhitespace))
                    {
                        return lastWhitespace;
                    }
                    
                    current = current.Parent;
                }

                return "";
            }

            private InvocationExpressionSyntax FormatChainCalls(InvocationExpressionSyntax node, string baseIndentation)
            {
                // Add standard indentation increment (matches ParameterAlignmentProcessor)
                var chainIndentation = baseIndentation + new string(' ', IndentationSpaces);
                
                // Format the expression recursively
                var formattedExpression = FormatMemberAccessChain(node.Expression, chainIndentation);
                
                // Update the invocation with formatted expression
                return node.WithExpression(formattedExpression);
            }

            private ExpressionSyntax FormatMemberAccessChain(ExpressionSyntax expression, string indentation)
            {
                if (expression is MemberAccessExpressionSyntax memberAccess)
                {
                    // Recursively format the left side
                    var formattedExpression = FormatMemberAccessChain(memberAccess.Expression, indentation);
                    
                    // Add line break and indentation before the dot operator
                    // Use Environment.NewLine to respect platform line ending convention
                    var operatorToken = memberAccess.OperatorToken
                        .WithLeadingTrivia(
                            SyntaxFactory.TriviaList(
                                SyntaxFactory.EndOfLine(Environment.NewLine),
                                SyntaxFactory.Whitespace(indentation)
                            )
                        )
                        .WithTrailingTrivia(SyntaxFactory.TriviaList());
                    
                    // Remove leading trivia from the name
                    var name = memberAccess.Name.WithLeadingTrivia(SyntaxFactory.TriviaList());
                    
                    return memberAccess
                        .WithExpression(formattedExpression)
                        .WithOperatorToken(operatorToken)
                        .WithName(name);
                }
                
                return expression;
            }
        }
    }
}
