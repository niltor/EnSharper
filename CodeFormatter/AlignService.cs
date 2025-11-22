using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
                newRoot = AlignAssignments(newRoot);

                var result = newRoot.ToFullString();
                
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
        /// Aligns parameters for constructors (>2 params), methods (>3 params), and primary constructors (>2 params)
        /// </summary>
        private SyntaxNode AlignParameters(SyntaxNode root)
        {
            var rewriter = new ParameterAlignmentRewriter();
            return rewriter.Visit(root);
        }

        /// <summary>
        /// Aligns assignments: local variables, class fields, and property assignments
        /// </summary>
        private SyntaxNode AlignAssignments(SyntaxNode root)
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
                    var indentation = DetectIndentation(node);
                    var newParameterList = FormatParameterList(node.ParameterList, indentation);
                    node = node.WithParameterList(newParameterList);
                }

                return base.VisitMethodDeclaration(node);
            }

            public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
            {
                // Align parameters if more than 2
                if (node.ParameterList.Parameters.Count > 2)
                {
                    var indentation = DetectIndentation(node);
                    var newParameterList = FormatParameterList(node.ParameterList, indentation);
                    node = node.WithParameterList(newParameterList);
                }

                return base.VisitConstructorDeclaration(node);
            }

            public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                // Handle primary constructors (C# 12+)
                if (node.ParameterList != null && node.ParameterList.Parameters.Count > 2)
                {
                    var indentation = DetectIndentation(node);
                    var newParameterList = FormatParameterList(node.ParameterList, indentation);
                    node = node.WithParameterList(newParameterList);
                }

                return base.VisitClassDeclaration(node);
            }

            public override SyntaxNode VisitRecordDeclaration(RecordDeclarationSyntax node)
            {
                // Handle record primary constructors
                if (node.ParameterList != null && node.ParameterList.Parameters.Count > 2)
                {
                    var indentation = DetectIndentation(node);
                    var newParameterList = FormatParameterList(node.ParameterList, indentation);
                    node = node.WithParameterList(newParameterList);
                }

                return base.VisitRecordDeclaration(node);
            }

            public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
            {
                // Handle struct primary constructors
                if (node.ParameterList != null && node.ParameterList.Parameters.Count > 2)
                {
                    var indentation = DetectIndentation(node);
                    var newParameterList = FormatParameterList(node.ParameterList, indentation);
                    node = node.WithParameterList(newParameterList);
                }

                return base.VisitStructDeclaration(node);
            }

            private string DetectIndentation(SyntaxNode node)
            {
                // Try to detect indentation from the node's leading trivia
                var leadingTrivia = node.GetLeadingTrivia();
                foreach (var trivia in leadingTrivia.Reverse())
                {
                    if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                    {
                        var text = trivia.ToFullString();
                        // Return the detected indentation plus one level (4 spaces default)
                        return text + "    ";
                    }
                }

                // Default to 8 spaces (2 levels of 4-space indentation)
                return "        ";
            }

            private ParameterListSyntax FormatParameterList(ParameterListSyntax parameterList, string indentation)
            {
                if (parameterList.Parameters.Count == 0)
                    return parameterList;

                // Check if parameters are already formatted (each on separate line)
                // by examining if they have newline trivia in their leading trivia
                bool alreadyFormatted = true;
                foreach (var param in parameterList.Parameters)
                {
                    var hasNewLine = param.GetLeadingTrivia().Any(t => 
                        t.IsKind(SyntaxKind.EndOfLineTrivia) || 
                        t.IsKind(SyntaxKind.CarriageReturnLineFeed));
                    if (!hasNewLine)
                    {
                        alreadyFormatted = false;
                        break;
                    }
                }

                // If already formatted correctly, don't modify
                if (alreadyFormatted)
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
        /// Rewriter for aligning variable and field assignments
        /// </summary>
        private class AssignmentAlignmentRewriter : CSharpSyntaxRewriter
        {
            public AssignmentAlignmentRewriter()
            {
            }

            // Handle local variables in blocks
            public override SyntaxNode VisitBlock(BlockSyntax node)
            {
                var statements = node.Statements.ToList();
                var newStatements = ProcessStatements(statements);
                return node.WithStatements(SyntaxFactory.List(newStatements));
            }

            // Handle class/struct fields
            public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                var members = node.Members.ToList();
                var newMembers = ProcessMembers(members);
                node = node.WithMembers(SyntaxFactory.List(newMembers));
                return base.VisitClassDeclaration(node);
            }

            public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
            {
                var members = node.Members.ToList();
                var newMembers = ProcessMembers(members);
                node = node.WithMembers(SyntaxFactory.List(newMembers));
                return base.VisitStructDeclaration(node);
            }

            private List<MemberDeclarationSyntax> ProcessMembers(List<MemberDeclarationSyntax> members)
            {
                var newMembers = new List<MemberDeclarationSyntax>();
                int i = 0;

                while (i < members.Count)
                {
                    // Find consecutive field declarations with initializers
                    // NOTE: Only fields are aligned, NOT properties
                    var group = new List<int> { i };
                    
                    if (members[i] is FieldDeclarationSyntax firstField && HasInitializer(firstField))
                    {
                        while (i + 1 < members.Count)
                        {
                            if (members[i + 1] is FieldDeclarationSyntax nextField && HasInitializer(nextField))
                            {
                                int currentLine = GetLineNumber(members[group.Last()]);
                                int nextLine = GetLineNumber(members[i + 1]);
                                
                                // Check if next member is consecutive
                                if (nextLine - currentLine <= 1)
                                {
                                    i++;
                                    group.Add(i);
                                }
                                else
                                {
                                    break;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }

                        // Align the group if it has more than one member
                        if (group.Count > 1)
                        {
                            var alignedGroup = AlignFieldGroup(members, group);
                            newMembers.AddRange(alignedGroup);
                        }
                        else
                        {
                            newMembers.Add(members[i]);
                        }
                    }
                    else
                    {
                        newMembers.Add(members[i]);
                    }

                    i++;
                }

                return newMembers;
            }

            private bool HasInitializer(FieldDeclarationSyntax field)
            {
                return field.Declaration.Variables.Any(v => v.Initializer != null);
            }

            private List<StatementSyntax> ProcessStatements(List<StatementSyntax> statements)
            {
                var newStatements = new List<StatementSyntax>();
                int i = 0;

                while (i < statements.Count)
                {
                    // Find consecutive assignment statements
                    var group = new List<int> { i };
                    
                    if (IsAssignmentStatement(statements[i]))
                    {
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
                            var alignedGroup = AlignStatementGroup(statements, group);
                            newStatements.AddRange(alignedGroup);
                        }
                        else
                        {
                            newStatements.Add(statements[i]);
                        }
                    }
                    else
                    {
                        newStatements.Add(statements[i]);
                    }

                    i++;
                }

                return newStatements;
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

            private int GetLineNumber(SyntaxNode node)
            {
                return node.GetLocation().GetLineSpan().StartLinePosition.Line;
            }

            private List<MemberDeclarationSyntax> AlignFieldGroup(List<MemberDeclarationSyntax> allMembers, List<int> indices)
            {
                var fields = indices.Select(idx => allMembers[idx] as FieldDeclarationSyntax).ToList();

                // Calculate alignment positions
                var typePositions = fields.Select(GetTypeEndPosition).ToList();
                var varPositions = fields.Select(GetVariableEndPosition).ToList();
                
                // Check if we have any positions to align (guard against empty collections)
                if (typePositions.Count == 0 || varPositions.Count == 0)
                    return new List<MemberDeclarationSyntax>(fields);
                
                var maxTypePos = typePositions.Max();
                var maxVarPos = varPositions.Max();

                var result = new List<MemberDeclarationSyntax>();
                for (int i = 0; i < fields.Count; i++)
                {
                    var field = fields[i];
                    var typePos = typePositions[i];
                    var varPos = varPositions[i];

                    // Align both type and variable name
                    field = AlignFieldDeclaration(field, maxTypePos - typePos, maxVarPos - varPos);
                    result.Add(field);
                }

                return result;
            }

            private List<StatementSyntax> AlignStatementGroup(List<StatementSyntax> allStatements, List<int> indices)
            {
                var statements = indices.Select(idx => allStatements[idx]).ToList();

                // Calculate alignment positions for both type and variable
                var typePositions = statements.Select(GetStatementTypeEndPosition).ToList();
                var varPositions = statements.Select(GetStatementVariableEndPosition).ToList();
                
                // Check if we have any positions to align (guard against empty collections)
                if (typePositions.Count == 0 || varPositions.Count == 0)
                    return statements;
                
                var maxTypePos = typePositions.Max();
                var maxVarPos = varPositions.Max();

                var result = new List<StatementSyntax>();
                for (int i = 0; i < statements.Count; i++)
                {
                    var statement = statements[i];
                    var typePos = typePositions[i];
                    var varPos = varPositions[i];

                    // Align both type and variable name
                    statement = AlignStatement(statement, maxTypePos - typePos, maxVarPos - varPos);
                    result.Add(statement);
                }

                return result;
            }

            private string GetTypeText(TypeSyntax type)
            {
                return type.ToString().Trim();
            }

            private int GetTypeEndPosition(FieldDeclarationSyntax field)
            {
                var typeText = GetTypeText(field.Declaration.Type);
                var modifiers = field.Modifiers.ToFullString();
                return modifiers.TrimEnd().Length + typeText.Length;
            }

            private int GetVariableEndPosition(FieldDeclarationSyntax field)
            {
                var firstVar = field.Declaration.Variables.First();
                var text = field.ToFullString();
                var varName = firstVar.Identifier.Text;
                var varIndex = text.IndexOf(varName);
                if (varIndex >= 0)
                {
                    return varIndex + varName.Length;
                }
                return GetTypeEndPosition(field) + varName.Length + 1;
            }

            private int GetStatementTypeEndPosition(StatementSyntax statement)
            {
                if (statement is LocalDeclarationStatementSyntax localDecl)
                {
                    return GetTypeText(localDecl.Declaration.Type).Length;
                }
                return 0;
            }

            private int GetStatementVariableEndPosition(StatementSyntax statement)
            {
                if (statement is LocalDeclarationStatementSyntax localDecl)
                {
                    var typeLen = GetTypeText(localDecl.Declaration.Type).Length;
                    var firstVar = localDecl.Declaration.Variables.First();
                    var varName = firstVar.Identifier.Text;
                    return typeLen + varName.Length + 1; // +1 for space
                }
                
                if (statement is ExpressionStatementSyntax expr && expr.Expression is AssignmentExpressionSyntax assignment)
                {
                    var leftText = assignment.Left.ToString().Trim();
                    return leftText.Length;
                }

                return 0;
            }

            private FieldDeclarationSyntax AlignFieldDeclaration(FieldDeclarationSyntax field, int typeSpaces, int varSpaces)
            {
                if (typeSpaces <= 0 && varSpaces <= 0)
                    return field;

                var newDeclaration = field.Declaration;

                // Add spaces after type if needed
                if (typeSpaces > 0)
                {
                    var newType = field.Declaration.Type.WithTrailingTrivia(
                        SyntaxFactory.Whitespace(new string(' ', typeSpaces) + " ")
                    );
                    newDeclaration = newDeclaration.WithType(newType);
                }

                // Add spaces after variable name if needed
                if (varSpaces > 0)
                {
                    var variables = newDeclaration.Variables;
                    var newVariables = new SeparatedSyntaxList<VariableDeclaratorSyntax>();

                    foreach (var variable in variables)
                    {
                        if (variable.Initializer != null)
                        {
                            var newVar = variable.WithIdentifier(
                                variable.Identifier.WithTrailingTrivia(
                                    SyntaxFactory.Whitespace(new string(' ', varSpaces))
                                )
                            );
                            newVariables = newVariables.Add(newVar);
                        }
                        else
                        {
                            newVariables = newVariables.Add(variable);
                        }
                    }

                    newDeclaration = newDeclaration.WithVariables(newVariables);
                }

                return field.WithDeclaration(newDeclaration);
            }

            private StatementSyntax AlignStatement(StatementSyntax statement, int typeSpaces, int varSpaces)
            {
                if (typeSpaces <= 0 && varSpaces <= 0)
                    return statement;

                if (statement is LocalDeclarationStatementSyntax localDecl)
                {
                    var newDeclaration = localDecl.Declaration;

                    // Add spaces after type if needed
                    if (typeSpaces > 0)
                    {
                        var newType = localDecl.Declaration.Type.WithTrailingTrivia(
                            SyntaxFactory.Whitespace(new string(' ', typeSpaces) + " ")
                        );
                        newDeclaration = newDeclaration.WithType(newType);
                    }

                    // Add spaces after variable name if needed
                    if (varSpaces > 0)
                    {
                        var variables = newDeclaration.Variables;
                        var newVariables = new SeparatedSyntaxList<VariableDeclaratorSyntax>();

                        foreach (var variable in variables)
                        {
                            if (variable.Initializer != null)
                            {
                                var newVar = variable.WithIdentifier(
                                    variable.Identifier.WithTrailingTrivia(
                                        SyntaxFactory.Whitespace(new string(' ', varSpaces))
                                    )
                                );
                                newVariables = newVariables.Add(newVar);
                            }
                            else
                            {
                                newVariables = newVariables.Add(variable);
                            }
                        }

                        newDeclaration = newDeclaration.WithVariables(newVariables);
                    }

                    return localDecl.WithDeclaration(newDeclaration);
                }
                else if (statement is ExpressionStatementSyntax expr && 
                         expr.Expression is AssignmentExpressionSyntax assignment)
                {
                    // For simple assignments (like aesAlg.Key = ...), add spaces before equals
                    if (varSpaces > 0)
                    {
                        var newLeft = assignment.Left.WithTrailingTrivia(
                            SyntaxFactory.Whitespace(new string(' ', varSpaces))
                        );
                        var newAssignment = assignment.WithLeft(newLeft);
                        return expr.WithExpression(newAssignment);
                    }
                }

                return statement;
            }
        }
    }
}
