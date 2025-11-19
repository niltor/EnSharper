using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
                var newRoot = AlignParameters(root);
                newRoot = AlignVariableAssignments(newRoot);

                return newRoot.ToFullString();
            }
            catch (Exception ex)
            {
                // If parsing fails, log the exception and return original code
                System.Diagnostics.Debug.WriteLine($"[AlignService.FormatCode] Exception: {ex.Message}");
                return code;
            }
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
        /// Aligns variable assignment = signs in consecutive lines
        /// </summary>
        private SyntaxNode AlignVariableAssignments(SyntaxNode root)
        {
            var rewriter = new AssignmentAlignmentRewriter();
            return rewriter.Visit(root);
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
                    var newParameterList = FormatParameterList(node.ParameterList, "        ");
                    node = node.WithParameterList(newParameterList);
                }

                return base.VisitMethodDeclaration(node);
            }

            public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
            {
                // Align parameters if more than 2
                if (node.ParameterList.Parameters.Count > 2)
                {
                    var newParameterList = FormatParameterList(node.ParameterList, "        ");
                    node = node.WithParameterList(newParameterList);
                }

                return base.VisitConstructorDeclaration(node);
            }

            private ParameterListSyntax FormatParameterList(ParameterListSyntax parameterList, string indentation)
            {
                if (parameterList.Parameters.Count == 0)
                    return parameterList;

                var newParameters = SyntaxFactory.SeparatedList<ParameterSyntax>();
                
                for (int i = 0; i < parameterList.Parameters.Count; i++)
                {
                    var parameter = parameterList.Parameters[i];
                    
                    // Add newline and indentation before each parameter
                    var leadingTrivia = SyntaxFactory.TriviaList(
                        SyntaxFactory.CarriageReturnLineFeed,
                        SyntaxFactory.Whitespace(indentation)
                    );

                    parameter = parameter.WithLeadingTrivia(leadingTrivia);
                    
                    // Remove trailing trivia except for the last parameter
                    if (i < parameterList.Parameters.Count - 1)
                    {
                        parameter = parameter.WithTrailingTrivia(SyntaxFactory.TriviaList());
                    }

                    newParameters = newParameters.Add(parameter);
                }

                // Add newline before closing parenthesis
                var closeParen = parameterList.CloseParenToken.WithLeadingTrivia(
                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)
                );

                return SyntaxFactory.ParameterList(
                    parameterList.OpenParenToken,
                    newParameters,
                    closeParen
                );
            }
        }

        /// <summary>
        /// Rewriter for aligning variable assignments
        /// </summary>
        private class AssignmentAlignmentRewriter : CSharpSyntaxRewriter
        {
            public override SyntaxNode VisitBlock(BlockSyntax node)
            {
                var statements = node.Statements.ToList();
                var newStatements = new List<StatementSyntax>();

                int i = 0;
                while (i < statements.Count)
                {
                    // Find consecutive assignment statements
                    var group = new List<int> { i };
                    
                    while (i + 1 < statements.Count)
                    {
                        int currentLine = GetLineNumber(statements[group.Last()]);
                        int nextLine = GetLineNumber(statements[i + 1]);
                        
                        // Check if next statement is consecutive and is an assignment
                        if (nextLine - currentLine <= 1 && IsAssignmentStatement(statements[i + 1]))
                        {
                            i++;
                            group.Add(i);
                        }
                        else
                        {
                            break;
                        }
                    }

                    // Align the group if it has more than one statement
                    if (group.Count > 1)
                    {
                        var alignedGroup = AlignAssignmentGroup(statements, group);
                        newStatements.AddRange(alignedGroup);
                    }
                    else
                    {
                        newStatements.Add(statements[i]);
                    }

                    i++;
                }

                return node.WithStatements(SyntaxFactory.List(newStatements));
            }

            private bool IsAssignmentStatement(StatementSyntax statement)
            {
                if (statement is LocalDeclarationStatementSyntax localDecl)
                {
                    return localDecl.Declaration.Variables.Any(v => v.Initializer != null);
                }
                
                if (statement is ExpressionStatementSyntax expr)
                {
                    return expr.Expression is AssignmentExpressionSyntax;
                }

                return false;
            }

            private int GetLineNumber(StatementSyntax statement)
            {
                return statement.GetLocation().GetLineSpan().StartLinePosition.Line;
            }

            private List<StatementSyntax> AlignAssignmentGroup(List<StatementSyntax> allStatements, List<int> indices)
            {
                // Find the position of the equals sign in each statement
                var positions = indices.Select(idx => GetEqualsPosition(allStatements[idx])).ToList();

                // Find maximum position
                int maxPos = positions.Max();

                // Align each statement
                var result = new List<StatementSyntax>();
                for (int i = 0; i < indices.Count; i++)
                {
                    var statement = allStatements[indices[i]];
                    int currentPos = positions[i];
                    int spacesToAdd = maxPos - currentPos;

                    if (spacesToAdd > 0)
                    {
                        statement = AddSpacesBeforeEquals(statement, spacesToAdd);
                    }

                    result.Add(statement);
                }

                return result;
            }

            private int GetEqualsPosition(StatementSyntax statement)
            {
                var text = statement.ToFullString();
                var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                if (lines.Length > 0)
                {
                    var firstLine = lines[0];
                    int equalsIndex = firstLine.IndexOf('=');
                    
                    if (equalsIndex >= 0)
                    {
                        // Count non-whitespace characters before the equals sign
                        return firstLine.Substring(0, equalsIndex).TrimEnd().Length;
                    }
                }

                return 0;
            }

            private StatementSyntax AddSpacesBeforeEquals(StatementSyntax statement, int spacesToAdd)
            {
                if (statement is LocalDeclarationStatementSyntax localDecl)
                {
                    var firstVariable = localDecl.Declaration.Variables.FirstOrDefault();
                    if (firstVariable?.Initializer != null)
                    {
                        var currentTrivia = firstVariable.Initializer.EqualsToken.LeadingTrivia;
                        var newTrivia = currentTrivia.Insert(0, SyntaxFactory.Whitespace(new string(' ', spacesToAdd)));
                        
                        var newInitializer = firstVariable.Initializer.WithEqualsToken(
                            firstVariable.Initializer.EqualsToken.WithLeadingTrivia(newTrivia)
                        );
                        
                        var newVariable = firstVariable.WithInitializer(newInitializer);
                        var newVariables = localDecl.Declaration.Variables.Replace(firstVariable, newVariable);
                        var newDeclaration = localDecl.Declaration.WithVariables(newVariables);
                        
                        return localDecl.WithDeclaration(newDeclaration);
                    }
                }
                else if (statement is ExpressionStatementSyntax expr && 
                         expr.Expression is AssignmentExpressionSyntax assignment)
                {
                    var currentTrivia = assignment.OperatorToken.LeadingTrivia;
                    var newTrivia = currentTrivia.Insert(0, SyntaxFactory.Whitespace(new string(' ', spacesToAdd)));
                    
                    var newAssignment = assignment.WithOperatorToken(
                        assignment.OperatorToken.WithLeadingTrivia(newTrivia)
                    );
                    
                    return expr.WithExpression(newAssignment);
                }

                return statement;
            }
        }
    }
}
