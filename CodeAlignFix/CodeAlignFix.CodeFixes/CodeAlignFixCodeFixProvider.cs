using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CodeAlignFix
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CodeAlignFixCodeFixProvider)), Shared]
    public class CodeAlignFixCodeFixProvider : CodeFixProvider
    {
        private const int DefaultIndentationSpaces = 4;
        private const string DefaultFallbackIndentation = "        "; // 8 spaces (2 levels)
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(
                    CodeAlignFixAnalyzer.AssignmentAlignmentId,
                    CodeAlignFixAnalyzer.ObjectInitializerAlignmentId,
                    CodeAlignFixAnalyzer.ParameterAlignmentId,
                    CodeAlignFixAnalyzer.ArgumentAlignmentId,
                    CodeAlignFixAnalyzer.ChainedMethodAlignmentId);
            }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            switch (diagnostic.Id)
            {
                case CodeAlignFixAnalyzer.AssignmentAlignmentId:
                    RegisterAssignmentAlignmentCodeFix(context, root, diagnostic, diagnosticSpan);
                    break;
                case CodeAlignFixAnalyzer.ObjectInitializerAlignmentId:
                    RegisterObjectInitializerAlignmentCodeFix(context, root, diagnostic, diagnosticSpan);
                    break;
                case CodeAlignFixAnalyzer.ParameterAlignmentId:
                    RegisterParameterAlignmentCodeFix(context, root, diagnostic, diagnosticSpan);
                    break;
                case CodeAlignFixAnalyzer.ArgumentAlignmentId:
                    RegisterArgumentAlignmentCodeFix(context, root, diagnostic, diagnosticSpan);
                    break;
                case CodeAlignFixAnalyzer.ChainedMethodAlignmentId:
                    RegisterChainedMethodAlignmentCodeFix(context, root, diagnostic, diagnosticSpan);
                    break;
            }
        }

        private void RegisterAssignmentAlignmentCodeFix(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic, TextSpan diagnosticSpan)
        {
            var node = root.FindNode(diagnosticSpan);
            
            // Try to find the statement or field
            var statement = node.AncestorsAndSelf().OfType<StatementSyntax>().FirstOrDefault();
            var field = node.AncestorsAndSelf().OfType<FieldDeclarationSyntax>().FirstOrDefault();

            if (statement != null || field != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: CodeFixResources.AlignAssignmentsCodeFixTitle,
                        createChangedDocument: c => AlignAssignmentsAsync(context.Document, root, node, c),
                        equivalenceKey: CodeFixResources.AlignAssignmentsCodeFixTitle),
                    diagnostic);
            }
        }

        private void RegisterObjectInitializerAlignmentCodeFix(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic, TextSpan diagnosticSpan)
        {
            var initializer = root.FindNode(diagnosticSpan).AncestorsAndSelf().OfType<InitializerExpressionSyntax>().FirstOrDefault();
            
            if (initializer != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: CodeFixResources.AlignObjectInitializerCodeFixTitle,
                        createChangedDocument: c => AlignObjectInitializerAsync(context.Document, root, initializer, c),
                        equivalenceKey: CodeFixResources.AlignObjectInitializerCodeFixTitle),
                    diagnostic);
            }
        }

        private void RegisterParameterAlignmentCodeFix(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic, TextSpan diagnosticSpan)
        {
            var parameterList = root.FindNode(diagnosticSpan).AncestorsAndSelf().OfType<ParameterListSyntax>().FirstOrDefault();
            
            if (parameterList != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: CodeFixResources.AlignParametersCodeFixTitle,
                        createChangedDocument: c => AlignParametersAsync(context.Document, root, parameterList, c),
                        equivalenceKey: CodeFixResources.AlignParametersCodeFixTitle),
                    diagnostic);
            }
        }

        private void RegisterArgumentAlignmentCodeFix(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic, TextSpan diagnosticSpan)
        {
            var argumentList = root.FindNode(diagnosticSpan).AncestorsAndSelf().OfType<ArgumentListSyntax>().FirstOrDefault();
            
            if (argumentList != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: CodeFixResources.AlignArgumentsCodeFixTitle,
                        createChangedDocument: c => AlignArgumentsAsync(context.Document, root, argumentList, c),
                        equivalenceKey: CodeFixResources.AlignArgumentsCodeFixTitle),
                    diagnostic);
            }
        }

        private void RegisterChainedMethodAlignmentCodeFix(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic, TextSpan diagnosticSpan)
        {
            var invocation = root.FindNode(diagnosticSpan).AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
            
            if (invocation != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: CodeFixResources.AlignChainedMethodsCodeFixTitle,
                        createChangedDocument: c => AlignChainedMethodsAsync(context.Document, root, invocation, c),
                        equivalenceKey: CodeFixResources.AlignChainedMethodsCodeFixTitle),
                    diagnostic);
            }
        }

        // Implementation methods
        private async Task<Document> AlignAssignmentsAsync(Document document, SyntaxNode root, SyntaxNode node, CancellationToken cancellationToken)
        {
            var statement = node.AncestorsAndSelf().OfType<StatementSyntax>().FirstOrDefault();
            var field = node.AncestorsAndSelf().OfType<FieldDeclarationSyntax>().FirstOrDefault();

            if (statement != null)
            {
                var block = statement.Parent as BlockSyntax;
                if (block == null)
                    return document;

                var newRoot = root.ReplaceNode(block, AlignStatementsInBlock(block));
                return document.WithSyntaxRoot(newRoot);
            }
            else if (field != null)
            {
                var parent = field.Parent;
                if (parent is ClassDeclarationSyntax classDecl)
                {
                    var newClass = AlignFieldsInClass(classDecl);
                    var newRoot = root.ReplaceNode(classDecl, newClass);
                    return document.WithSyntaxRoot(newRoot);
                }
                else if (parent is StructDeclarationSyntax structDecl)
                {
                    var newStruct = AlignFieldsInStruct(structDecl);
                    var newRoot = root.ReplaceNode(structDecl, newStruct);
                    return document.WithSyntaxRoot(newRoot);
                }
            }

            return document;
        }

        private async Task<Document> AlignObjectInitializerAsync(Document document, SyntaxNode root, InitializerExpressionSyntax initializer, CancellationToken cancellationToken)
        {
            var newInitializer = AlignObjectInitializer(initializer);
            var newRoot = root.ReplaceNode(initializer, newInitializer);
            return document.WithSyntaxRoot(newRoot);
        }

        private async Task<Document> AlignParametersAsync(Document document, SyntaxNode root, ParameterListSyntax parameterList, CancellationToken cancellationToken)
        {
            var parent = parameterList.Parent;
            var indentation = DetectIndentation(parent);
            var newParameterList = FormatParameterList(parameterList, indentation);
            var newRoot = root.ReplaceNode(parameterList, newParameterList);
            return document.WithSyntaxRoot(newRoot);
        }

        private async Task<Document> AlignArgumentsAsync(Document document, SyntaxNode root, ArgumentListSyntax argumentList, CancellationToken cancellationToken)
        {
            var parent = argumentList.Parent;
            var indentation = DetectIndentation(parent);
            var newArgumentList = FormatArgumentList(argumentList, indentation);
            var newRoot = root.ReplaceNode(argumentList, newArgumentList);
            return document.WithSyntaxRoot(newRoot);
        }

        private async Task<Document> AlignChainedMethodsAsync(Document document, SyntaxNode root, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
        {
            var indentation = DetectIndentation(invocation);
            var newInvocation = FormatChainedMethods(invocation, indentation);
            var newRoot = root.ReplaceNode(invocation, newInvocation);
            return document.WithSyntaxRoot(newRoot);
        }

        // Helper methods for alignment
        private BlockSyntax AlignStatementsInBlock(BlockSyntax block)
        {
            var statements = block.Statements.ToList();
            var newStatements = new List<StatementSyntax>();
            int i = 0;

            while (i < statements.Count)
            {
                if (!IsAssignmentStatement(statements[i]))
                {
                    newStatements.Add(statements[i]);
                    i++;
                    continue;
                }

                var group = new List<StatementSyntax> { statements[i] };
                bool isLocalDeclaration = statements[i] is LocalDeclarationStatementSyntax;

                for (int j = i + 1; j < statements.Count; j++)
                {
                    if (!IsAssignmentStatement(statements[j]))
                        break;

                    bool nextIsLocalDecl = statements[j] is LocalDeclarationStatementSyntax;
                    if (nextIsLocalDecl != isLocalDeclaration)
                        break;

                    int currentLine = GetLineNumber(statements[j - 1]);
                    int nextLine = GetLineNumber(statements[j]);

                    if (nextLine - currentLine > 1 || HasBlankLineBetweenNodes(statements[j - 1], statements[j]))
                        break;

                    group.Add(statements[j]);
                }

                if (group.Count > 1)
                {
                    newStatements.AddRange(AlignStatementGroup(group));
                }
                else
                {
                    newStatements.Add(statements[i]);
                }

                i += group.Count;
            }

            return block.WithStatements(SyntaxFactory.List(newStatements));
        }

        private ClassDeclarationSyntax AlignFieldsInClass(ClassDeclarationSyntax classDecl)
        {
            var members = classDecl.Members.ToList();
            var newMembers = AlignMemberFields(members);
            return classDecl.WithMembers(SyntaxFactory.List(newMembers));
        }

        private StructDeclarationSyntax AlignFieldsInStruct(StructDeclarationSyntax structDecl)
        {
            var members = structDecl.Members.ToList();
            var newMembers = AlignMemberFields(members);
            return structDecl.WithMembers(SyntaxFactory.List(newMembers));
        }

        private List<MemberDeclarationSyntax> AlignMemberFields(List<MemberDeclarationSyntax> members)
        {
            var newMembers = new List<MemberDeclarationSyntax>();
            int i = 0;

            while (i < members.Count)
            {
                if (!(members[i] is FieldDeclarationSyntax firstField) || !HasInitializer(firstField))
                {
                    newMembers.Add(members[i]);
                    i++;
                    continue;
                }

                var group = new List<FieldDeclarationSyntax> { firstField };

                for (int j = i + 1; j < members.Count; j++)
                {
                    if (!(members[j] is FieldDeclarationSyntax nextField) || !HasInitializer(nextField))
                        break;

                    int currentLine = GetLineNumber(members[j - 1]);
                    int nextLine = GetLineNumber(members[j]);

                    if (nextLine - currentLine > 1 || HasBlankLineBetweenNodes(members[j - 1], members[j]))
                        break;

                    group.Add(nextField);
                }

                if (group.Count > 1)
                {
                    newMembers.AddRange(AlignFieldGroup(group));
                }
                else
                {
                    newMembers.Add(members[i]);
                }

                i += group.Count;
            }

            return newMembers;
        }

        private List<StatementSyntax> AlignStatementGroup(List<StatementSyntax> statements)
        {
            var typePositions = statements.Select(GetStatementTypeEndPosition).ToList();
            var maxTypePos = typePositions.Max();

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

            if (varPositions.Count == 0)
                return statements;

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

        private List<MemberDeclarationSyntax> AlignFieldGroup(List<FieldDeclarationSyntax> fields)
        {
            var typePositions = fields.Select(GetTypeEndPosition).ToList();
            var maxTypePos = typePositions.Max();

            var varPositions = fields
                .Select(field => field.Declaration.Variables.FirstOrDefault())
                .Where(firstVar => firstVar != null)
                .Select(firstVar => maxTypePos + 1 + firstVar.Identifier.Text.Length)
                .ToList();

            if (varPositions.Count == 0)
                return fields.Cast<MemberDeclarationSyntax>().ToList();

            var maxVarPos = varPositions.Max();

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

        private InitializerExpressionSyntax AlignObjectInitializer(InitializerExpressionSyntax initializer)
        {
            var assignments = initializer.Expressions.OfType<AssignmentExpressionSyntax>().ToList();
            
            if (assignments.Count == 0)
                return initializer;

            var propertyPositions = assignments.Select(a => a.Left.ToString().Trim().Length).ToList();
            var maxPropertyPos = propertyPositions.Max();

            var alignedExpressions = new Dictionary<ExpressionSyntax, ExpressionSyntax>();

            for (int i = 0; i < assignments.Count; i++)
            {
                var assignment = assignments[i];
                var propertyPos = propertyPositions[i];
                var spacesToAdd = maxPropertyPos - propertyPos;

                var newLeft = assignment.Left.WithTrailingTrivia(
                    SyntaxFactory.Whitespace(new string(' ', spacesToAdd + 1))
                );
                var newAssignment = assignment.WithLeft(newLeft);
                alignedExpressions[assignment] = newAssignment;
            }

            return initializer.ReplaceNodes(
                alignedExpressions.Keys,
                (oldNode, _) => alignedExpressions[oldNode]
            );
        }

        private ParameterListSyntax FormatParameterList(ParameterListSyntax parameterList, string indentation)
        {
            if (parameterList.Parameters.Count == 0)
                return parameterList;

            var newParameters = SyntaxFactory.SeparatedList<ParameterSyntax>();
            foreach (var parameter in parameterList.Parameters)
            {
                var leadingTrivia = SyntaxFactory.TriviaList(
                    SyntaxFactory.CarriageReturnLineFeed,
                    SyntaxFactory.Whitespace(indentation)
                );

                var updatedParam = parameter.WithLeadingTrivia(leadingTrivia)
                    .WithTrailingTrivia(SyntaxFactory.TriviaList());
                newParameters = newParameters.Add(updatedParam);
            }

            var baseIndentation = indentation.Length >= 4
                ? indentation.Substring(0, indentation.Length - 4)
                : string.Empty;

            var closeParen = parameterList.CloseParenToken.WithLeadingTrivia(
                SyntaxFactory.TriviaList(
                    SyntaxFactory.CarriageReturnLineFeed,
                    SyntaxFactory.Whitespace(baseIndentation)
                )
            ).WithTrailingTrivia(SyntaxFactory.TriviaList());

            return SyntaxFactory.ParameterList(
                parameterList.OpenParenToken.WithTrailingTrivia(SyntaxFactory.TriviaList()),
                newParameters,
                closeParen
            );
        }

        private ArgumentListSyntax FormatArgumentList(ArgumentListSyntax argumentList, string indentation)
        {
            if (argumentList.Arguments.Count == 0)
                return argumentList;

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

        private InvocationExpressionSyntax FormatChainedMethods(InvocationExpressionSyntax invocation, string baseIndentation)
        {
            var chainIndentation = baseIndentation + "    ";
            var formattedExpression = FormatMemberAccessChain(invocation.Expression, chainIndentation);
            return invocation.WithExpression(formattedExpression);
        }

        private ExpressionSyntax FormatMemberAccessChain(ExpressionSyntax expression, string indentation)
        {
            if (expression is MemberAccessExpressionSyntax memberAccess)
            {
                var formattedExpression = FormatMemberAccessChain(memberAccess.Expression, indentation);

                if (memberAccess.Parent is InvocationExpressionSyntax)
                {
                    var operatorToken = memberAccess.OperatorToken
                        .WithLeadingTrivia(
                            SyntaxFactory.TriviaList(
                                SyntaxFactory.EndOfLine(Environment.NewLine),
                                SyntaxFactory.Whitespace(indentation)
                            )
                        )
                        .WithTrailingTrivia(SyntaxFactory.TriviaList());

                    var name = memberAccess.Name.WithLeadingTrivia(SyntaxFactory.TriviaList());

                    return memberAccess
                        .WithExpression(formattedExpression)
                        .WithOperatorToken(operatorToken)
                        .WithName(name);
                }
                else
                {
                    return memberAccess.WithExpression(formattedExpression);
                }
            }

            return expression;
        }

        // Helper methods
        private bool IsAssignmentStatement(StatementSyntax statement)
        {
            if (statement is LocalDeclarationStatementSyntax localDecl)
                return localDecl.Declaration.Variables.Any(v => v.Initializer != null);

            if (statement is ExpressionStatementSyntax expr)
                return expr.Expression is AssignmentExpressionSyntax;

            return false;
        }

        private bool HasInitializer(FieldDeclarationSyntax field)
        {
            return field.Declaration.Variables.Any(v => v.Initializer != null);
        }

        private int GetLineNumber(SyntaxNode node)
        {
            return node.GetLocation().GetLineSpan().StartLinePosition.Line;
        }

        private bool HasBlankLineBetweenNodes(SyntaxNode previous, SyntaxNode next)
        {
            if (previous == null || next == null)
                return false;

            var combinedTrivia = previous.GetTrailingTrivia().Concat(next.GetLeadingTrivia());
            int consecutiveLineBreaks = 0;

            foreach (var trivia in combinedTrivia)
            {
                if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    consecutiveLineBreaks++;
                    if (consecutiveLineBreaks >= 2)
                        return true;
                }
                else if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                {
                    consecutiveLineBreaks = 0;
                }
            }

            return false;
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

        private int GetTypeEndPosition(FieldDeclarationSyntax field)
        {
            var typeText = field.Declaration.Type.ToString().Trim();
            var modifiers = field.Modifiers.ToFullString();

            return string.IsNullOrEmpty(modifiers.Trim())
                ? typeText.Length
                : modifiers.TrimEnd().Length + 1 + typeText.Length;
        }

        private string DetectIndentation(SyntaxNode node)
        {
            var current = node;
            while (current != null)
            {
                var leadingTrivia = current.GetLeadingTrivia();

                var lastWhitespace = new System.Text.StringBuilder();
                for (int i = 0; i < leadingTrivia.Count; i++)
                {
                    var trivia = leadingTrivia[i];
                    if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                    {
                        lastWhitespace.Clear();
                    }
                    else if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                    {
                        lastWhitespace.Append(trivia.ToFullString());
                    }
                }

                if (lastWhitespace.Length > 0)
                {
                    return lastWhitespace.ToString() + new string(' ', DefaultIndentationSpaces);
                }

                current = current.Parent;
            }

            return DefaultFallbackIndentation;
        }
    }
}
