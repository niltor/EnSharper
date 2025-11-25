using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeFormatter.Processors
{
    /// <summary>
    /// Aligns declarations/assignments when they form consecutive groups.
    /// </summary>
    internal class AssignmentAlignmentProcessor : IAlignmentProcessor
    {
        private readonly int maxAlignmentGap;

        public AssignmentAlignmentProcessor(int maxAlignmentGap)
        {
            this.maxAlignmentGap = maxAlignmentGap;
        }

        public SyntaxNode Apply(SyntaxNode root)
        {
            var rewriter = new AssignmentAlignmentRewriter(maxAlignmentGap);
            return rewriter.Visit(root);
        }

        private class AssignmentAlignmentRewriter : CSharpSyntaxRewriter
        {
            private readonly int maxAlignmentGap;

            public AssignmentAlignmentRewriter(int maxAlignmentGap)
            {
                this.maxAlignmentGap = maxAlignmentGap;
            }

            public override SyntaxNode VisitBlock(BlockSyntax node)
            {
                var statements = node.Statements.ToList();
                var newStatements = ProcessStatements(statements);
                return node.WithStatements(SyntaxFactory.List(newStatements));
            }

            public override SyntaxNode VisitUsingStatement(UsingStatementSyntax node)
            {
                // Process statements inside using statement block
                if (node.Statement is BlockSyntax block)
                {
                    var statements = block.Statements.ToList();
                    var newStatements = ProcessStatements(statements);
                    var newBlock = block.WithStatements(SyntaxFactory.List(newStatements));
                    node = node.WithStatement(newBlock);
                }
                return base.VisitUsingStatement(node);
            }

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
                    var group = new List<int> { i };

                    if (members[i] is FieldDeclarationSyntax firstField && HasInitializer(firstField))
                    {
                        while (i + 1 < members.Count)
                        {
                            if (members[i + 1] is FieldDeclarationSyntax nextField && HasInitializer(nextField))
                            {
                                int currentLine = GetLineNumber(members[group.Last()]);
                                int nextLine = GetLineNumber(members[i + 1]);

                                if (nextLine - currentLine <= 1 &&
                                    !HasBlankLineBetweenNodes(members[group.Last()], members[i + 1]))
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
                    var group = new List<int> { i };

                    if (IsAssignmentStatement(statements[i]))
                    {
                        bool isLocalDeclaration = statements[i] is LocalDeclarationStatementSyntax;

                        while (i + 1 < statements.Count)
                        {
                            int currentLine = GetLineNumber(statements[group.Last()]);
                            int nextLine = GetLineNumber(statements[i + 1]);

                            bool nextIsLocalDecl = statements[i + 1] is LocalDeclarationStatementSyntax;
                            if (nextLine - currentLine <= 1 &&
                                IsAssignmentStatement(statements[i + 1]) &&
                                nextIsLocalDecl == isLocalDeclaration &&
                                !HasBlankLineBetweenNodes(statements[group.Last()], statements[i + 1]))
                            {
                                i++;
                                group.Add(i);
                            }
                            else
                            {
                                break;
                            }
                        }

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
                var fields = indices
                    .Select(idx => allMembers[idx] as FieldDeclarationSyntax)
                    .Where(f => f != null && f.Declaration.Variables.Count > 0)
                    .ToList();

                if (fields.Count == 0)
                {
                    return new List<MemberDeclarationSyntax>();
                }

                var typePositions = fields.Select(GetTypeEndPosition).ToList();
                if (typePositions.Count == 0)
                {
                    return new List<MemberDeclarationSyntax>(fields);
                }

                var maxTypePos = typePositions.Max();

                if (maxAlignmentGap > 0)
                {
                    var minTypePos = typePositions.Min();
                    if (maxTypePos - minTypePos > maxAlignmentGap)
                    {
                        Logger.LogDebug("AlignService", $"Type alignment gap ({maxTypePos - minTypePos}) exceeds maximum ({maxAlignmentGap}), skipping field group");
                        return new List<MemberDeclarationSyntax>(fields);
                    }
                }

                var varPositions = new List<int>();
                foreach (var field in fields)
                {
                    var firstVar = field.Declaration.Variables.FirstOrDefault();
                    if (firstVar == null)
                    {
                        continue;
                    }

                    var varName = firstVar.Identifier.Text;
                    var varEndPos = maxTypePos + 1 + varName.Length;
                    varPositions.Add(varEndPos);
                }

                var maxVarPos = varPositions.Max();

                // ??????
                Logger.LogDebug("AlignService", $"Field alignment: types [{typePositions.Min()}-{maxTypePos}], vars [{varPositions.Min()}-{maxVarPos}]");
                for (int i = 0; i < fields.Count; i++)
                {
                    var field = fields[i];
                    var varName = field.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "";
                    Logger.LogDebug("AlignService", $"  Field {i}: type@{typePositions[i]} var'{varName}'@{varPositions[i]} ? typeSpaces={maxTypePos - typePositions[i]} varSpaces={maxVarPos - varPositions[i]}");
                }

                var result = new List<MemberDeclarationSyntax>();
                for (int i = 0; i < fields.Count; i++)
                {
                    var field = fields[i];
                    var typePos = typePositions[i];
                    var varPos = varPositions[i];

                    field = AlignFieldDeclaration(field, maxTypePos - typePos, maxVarPos - varPos);
                    result.Add(field);
                }

                return result;
            }

            private List<StatementSyntax> AlignStatementGroup(List<StatementSyntax> allStatements, List<int> indices)
            {
                var statements = indices.Select(idx => allStatements[idx]).ToList();

                var validStatements = new List<StatementSyntax>();
                foreach (var stmt in statements)
                {
                    if (stmt is LocalDeclarationStatementSyntax localDecl)
                    {
                        if (localDecl.Declaration.Variables.Count > 0)
                        {
                            validStatements.Add(stmt);
                        }
                    }
                    else if (stmt is ExpressionStatementSyntax expr &&
                             expr.Expression is AssignmentExpressionSyntax)
                    {
                        validStatements.Add(stmt);
                    }
                }

                if (validStatements.Count == 0)
                {
                    return statements;
                }

                statements = validStatements;

                var typePositions = statements.Select(GetStatementTypeEndPosition).ToList();
                if (typePositions.Count == 0)
                {
                    return statements;
                }

                var maxTypePos = typePositions.Max();

                if (maxAlignmentGap > 0)
                {
                    var minTypePos = typePositions.Min();
                    if (maxTypePos - minTypePos > maxAlignmentGap)
                    {
                        Logger.LogDebug("AlignService", $"Type alignment gap ({maxTypePos - minTypePos}) exceeds maximum ({maxAlignmentGap}), skipping statement group");
                        return statements;
                    }
                }

                var varPositions = new List<int>();
                foreach (var statement in statements)
                {
                    var varEndPos = 0;
                    if (statement is LocalDeclarationStatementSyntax localDecl)
                    {
                        var firstVar = localDecl.Declaration.Variables.First();
                        var varName = firstVar.Identifier.Text;
                        varEndPos = maxTypePos + 1 + varName.Length;
                    }
                    else if (statement is ExpressionStatementSyntax expr &&
                             expr.Expression is AssignmentExpressionSyntax assignment)
                    {
                        var leftText = assignment.Left.ToString().Trim();
                        varEndPos = leftText.Length;
                    }

                    varPositions.Add(varEndPos);
                }

                var maxVarPos = varPositions.Max();

                var result = new List<StatementSyntax>();
                for (int i = 0; i < statements.Count; i++)
                {
                    var statement = statements[i];
                    var typePos = typePositions[i];
                    var varPos = varPositions[i];

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

                if (string.IsNullOrEmpty(modifiers.Trim()))
                {
                    return typeText.Length;
                }
                else
                {
                    return modifiers.TrimEnd().Length + 1 + typeText.Length;
                }
            }

            private int GetStatementTypeEndPosition(StatementSyntax statement)
            {
                if (statement is LocalDeclarationStatementSyntax localDecl)
                {
                    var statementSpan = localDecl.GetLocation().GetLineSpan();
                    var typeSpan = localDecl.Declaration.Type.GetLocation().GetLineSpan();

                    if (statementSpan.Path == typeSpan.Path &&
                        statementSpan.StartLinePosition.Line == typeSpan.StartLinePosition.Line)
                    {
                        var relativeEnd = typeSpan.EndLinePosition.Character - statementSpan.StartLinePosition.Character;
                        return Math.Max(0, relativeEnd);
                    }

                    return Math.Max(0, typeSpan.EndLinePosition.Character);
                }

                return 0;
            }

            private bool HasBlankLineBetweenNodes(SyntaxNode previous, SyntaxNode next)
            {
                if (previous == null || next == null)
                {
                    return false;
                }

                var combinedTrivia = previous.GetTrailingTrivia().Concat(next.GetLeadingTrivia());
                int consecutiveLineBreaks = 0;

                foreach (var trivia in combinedTrivia)
                {
                    if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                    {
                        consecutiveLineBreaks++;
                        if (consecutiveLineBreaks >= 2)
                        {
                            return true;
                        }
                    }
                    else if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                    {
                        continue;
                    }
                    else
                    {
                        consecutiveLineBreaks = 0;
                    }
                }

                return false;
            }

            private FieldDeclarationSyntax AlignFieldDeclaration(FieldDeclarationSyntax field, int typeSpaces, int varSpaces)
            {
                var newDeclaration = field.Declaration;

                var newType = field.Declaration.Type.WithTrailingTrivia(
                    SyntaxFactory.Whitespace(new string(' ', Math.Max(0, typeSpaces) + 1))
                );
                newDeclaration = newDeclaration.WithType(newType);

                var variables = newDeclaration.Variables;
                var newVariables = new SeparatedSyntaxList<VariableDeclaratorSyntax>();

                foreach (var variable in variables)
                {
                    if (variable.Initializer != null)
                    {
                        var newVar = variable.WithIdentifier(
                            variable.Identifier.WithTrailingTrivia(
                                SyntaxFactory.Whitespace(new string(' ', Math.Max(0, varSpaces) + 1))
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

                return field.WithDeclaration(newDeclaration);
            }

            private StatementSyntax AlignStatement(StatementSyntax statement, int typeSpaces, int varSpaces)
            {
                if (statement is LocalDeclarationStatementSyntax localDecl)
                {
                    var newDeclaration = localDecl.Declaration;
                    var newType = localDecl.Declaration.Type.WithTrailingTrivia(
                        SyntaxFactory.Whitespace(new string(' ', Math.Max(0, typeSpaces) + 1))
                    );
                    newDeclaration = newDeclaration.WithType(newType);

                    var variables = newDeclaration.Variables;
                    var newVariables = new SeparatedSyntaxList<VariableDeclaratorSyntax>();

                    foreach (var variable in variables)
                    {
                        if (variable.Initializer != null)
                        {
                            var newVar = variable.WithIdentifier(
                                variable.Identifier.WithTrailingTrivia(
                                    SyntaxFactory.Whitespace(new string(' ', Math.Max(0, varSpaces) + 1))
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

                    return localDecl.WithDeclaration(newDeclaration);
                }
                else if (statement is ExpressionStatementSyntax expr &&
                         expr.Expression is AssignmentExpressionSyntax assignment)
                {
                    var newLeft = assignment.Left.WithTrailingTrivia(
                        SyntaxFactory.Whitespace(new string(' ', Math.Max(0, varSpaces) + 1))
                    );
                    var newAssignment = assignment.WithLeft(newLeft);
                    return expr.WithExpression(newAssignment);
                }

                return statement;
            }
        }
    }
}
