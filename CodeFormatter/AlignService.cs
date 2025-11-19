using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
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
        /// <summary>
        /// Formats the given code with alignment
        /// </summary>
        public string FormatCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return code;

            try
            {
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetRoot();

                // Apply alignment transformations
                var newRoot = AlignVariableAssignments(root);
                newRoot = AlignParameters(newRoot);

                return newRoot.ToFullString();
            }
            catch (Exception)
            {
                // If parsing fails, return original code
                return code;
            }
        }

        /// <summary>
        /// Aligns variable assignment = signs
        /// </summary>
        private SyntaxNode AlignVariableAssignments(SyntaxNode root)
        {
            var rewriter = new AssignmentAlignmentRewriter();
            return rewriter.Visit(root);
        }

        /// <summary>
        /// Aligns parameters for constructors (>2 params) and methods (>3 params)
        /// </summary>
        private SyntaxNode AlignParameters(SyntaxNode root)
        {
            var rewriter = new ParameterAlignmentRewriter();
            return rewriter.Visit(root);
        }

        /// <summary>
        /// Rewriter for aligning variable assignments
        /// </summary>
        private class AssignmentAlignmentRewriter : CSharpSyntaxRewriter
        {
            public override SyntaxNode VisitBlock(BlockSyntax node)
            {
                // Group consecutive variable declarations/assignments
                var statements = node.Statements.ToList();
                var alignedStatements = new List<StatementSyntax>();

                for (int i = 0; i < statements.Count; i++)
                {
                    var currentGroup = new List<StatementSyntax> { statements[i] };
                    
                    // Collect consecutive assignments/declarations
                    while (i + 1 < statements.Count && 
                           IsAssignmentOrDeclaration(statements[i + 1]) &&
                           AreOnConsecutiveLines(statements[i], statements[i + 1]))
                    {
                        i++;
                        currentGroup.Add(statements[i]);
                    }

                    // Align the group if it has more than one assignment
                    if (currentGroup.Count > 1 && currentGroup.All(IsAssignmentOrDeclaration))
                    {
                        alignedStatements.AddRange(AlignAssignmentGroup(currentGroup));
                    }
                    else
                    {
                        alignedStatements.AddRange(currentGroup);
                    }
                }

                return node.WithStatements(SyntaxFactory.List(alignedStatements));
            }

            private bool IsAssignmentOrDeclaration(StatementSyntax statement)
            {
                return statement is LocalDeclarationStatementSyntax ||
                       (statement is ExpressionStatementSyntax expr && 
                        expr.Expression is AssignmentExpressionSyntax);
            }

            private bool AreOnConsecutiveLines(StatementSyntax first, StatementSyntax second)
            {
                var firstLine = first.GetLocation().GetLineSpan().EndLinePosition.Line;
                var secondLine = second.GetLocation().GetLineSpan().StartLinePosition.Line;
                return secondLine - firstLine <= 1;
            }

            private List<StatementSyntax> AlignAssignmentGroup(List<StatementSyntax> group)
            {
                // Find the maximum position for the = sign
                int maxPosition = 0;

                foreach (var statement in group)
                {
                    int position = GetAssignmentPosition(statement);
                    if (position > maxPosition)
                        maxPosition = position;
                }

                // Align each statement
                var result = new List<StatementSyntax>();
                foreach (var statement in group)
                {
                    result.Add(AlignStatement(statement, maxPosition));
                }

                return result;
            }

            private int GetAssignmentPosition(StatementSyntax statement)
            {
                if (statement is LocalDeclarationStatementSyntax localDecl)
                {
                    var variable = localDecl.Declaration.Variables.FirstOrDefault();
                    if (variable?.Initializer != null)
                    {
                        var textBeforeEquals = localDecl.ToFullString().Substring(0,
                            localDecl.ToFullString().IndexOf('='));
                        return textBeforeEquals.TrimEnd().Length;
                    }
                }
                else if (statement is ExpressionStatementSyntax expr &&
                         expr.Expression is AssignmentExpressionSyntax assignment)
                {
                    var textBeforeEquals = assignment.Left.ToFullString();
                    return textBeforeEquals.TrimEnd().Length;
                }

                return 0;
            }

            private StatementSyntax AlignStatement(StatementSyntax statement, int targetPosition)
            {
                if (statement is LocalDeclarationStatementSyntax localDecl)
                {
                    var variable = localDecl.Declaration.Variables.FirstOrDefault();
                    if (variable?.Initializer != null)
                    {
                        int currentPosition = GetAssignmentPosition(statement);
                        int spacesToAdd = targetPosition - currentPosition;

                        if (spacesToAdd > 0)
                        {
                            var newVariable = variable.WithInitializer(
                                variable.Initializer.WithEqualsToken(
                                    variable.Initializer.EqualsToken.WithLeadingTrivia(
                                        SyntaxFactory.Whitespace(new string(' ', spacesToAdd))
                                    )
                                )
                            );

                            var newDeclaration = localDecl.Declaration.WithVariables(
                                SyntaxFactory.SingletonSeparatedList(newVariable)
                            );

                            return localDecl.WithDeclaration(newDeclaration);
                        }
                    }
                }
                else if (statement is ExpressionStatementSyntax expr &&
                         expr.Expression is AssignmentExpressionSyntax assignment)
                {
                    int currentPosition = GetAssignmentPosition(statement);
                    int spacesToAdd = targetPosition - currentPosition;

                    if (spacesToAdd > 0)
                    {
                        var newAssignment = assignment.WithOperatorToken(
                            assignment.OperatorToken.WithLeadingTrivia(
                                SyntaxFactory.Whitespace(new string(' ', spacesToAdd))
                            )
                        );

                        return expr.WithExpression(newAssignment);
                    }
                }

                return statement;
            }
        }

        /// <summary>
        /// Rewriter for aligning method/constructor parameters
        /// </summary>
        private class ParameterAlignmentRewriter : CSharpSyntaxRewriter
        {
            public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                // Align parameters if more than 3
                if (node.ParameterList.Parameters.Count > 3)
                {
                    var newParameterList = AlignParameterList(node.ParameterList);
                    node = node.WithParameterList(newParameterList);
                }

                return base.VisitMethodDeclaration(node);
            }

            public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
            {
                // Align parameters if more than 2
                if (node.ParameterList.Parameters.Count > 2)
                {
                    var newParameterList = AlignParameterList(node.ParameterList);
                    node = node.WithParameterList(newParameterList);
                }

                return base.VisitConstructorDeclaration(node);
            }

            private ParameterListSyntax AlignParameterList(ParameterListSyntax parameterList)
            {
                var parameters = parameterList.Parameters;
                if (parameters.Count == 0)
                    return parameterList;

                var newParameters = new List<SyntaxNode>();

                for (int i = 0; i < parameters.Count; i++)
                {
                    var parameter = parameters[i];

                    // Add leading trivia for indentation (newline + 4 spaces)
                    var leadingTrivia = SyntaxFactory.TriviaList(
                        SyntaxFactory.CarriageReturnLineFeed,
                        SyntaxFactory.Whitespace("    ")
                    );

                    parameter = parameter.WithLeadingTrivia(leadingTrivia);

                    // Handle trailing comma
                    if (i < parameters.Count - 1)
                    {
                        parameter = parameter.WithTrailingTrivia(SyntaxFactory.TriviaList());
                    }

                    newParameters.Add(parameter);

                    // Add separator if not the last parameter
                    if (i < parameters.Count - 1)
                    {
                        newParameters.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
                    }
                }

                // Add closing parenthesis on new line
                var closeParen = parameterList.CloseParenToken.WithLeadingTrivia(
                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)
                );

                return SyntaxFactory.ParameterList(
                    parameterList.OpenParenToken,
                    SyntaxFactory.SeparatedList<ParameterSyntax>(newParameters),
                    closeParen
                );
            }
        }
    }
}
