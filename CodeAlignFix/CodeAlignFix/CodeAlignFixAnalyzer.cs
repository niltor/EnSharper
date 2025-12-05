using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeAlignFix
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CodeAlignFixAnalyzer : DiagnosticAnalyzer
    {
        // Diagnostic IDs
        public const string AssignmentAlignmentId = "CAF001";
        public const string ObjectInitializerAlignmentId = "CAF002";
        public const string ParameterAlignmentId = "CAF003";
        public const string ArgumentAlignmentId = "CAF004";
        public const string ChainedMethodAlignmentId = "CAF005";

        private const string Category = "Formatting";

        // Assignment Alignment Rule
        private static readonly DiagnosticDescriptor AssignmentAlignmentRule = new DiagnosticDescriptor(
            AssignmentAlignmentId,
            new LocalizableResourceString(nameof(Resources.AssignmentAlignmentTitle), Resources.ResourceManager, typeof(Resources)),
            new LocalizableResourceString(nameof(Resources.AssignmentAlignmentMessageFormat), Resources.ResourceManager, typeof(Resources)),
            Category,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: new LocalizableResourceString(nameof(Resources.AssignmentAlignmentDescription), Resources.ResourceManager, typeof(Resources)),
            customTags: WellKnownDiagnosticTags.Unnecessary);

        // Object Initializer Alignment Rule
        private static readonly DiagnosticDescriptor ObjectInitializerAlignmentRule = new DiagnosticDescriptor(
            ObjectInitializerAlignmentId,
            new LocalizableResourceString(nameof(Resources.ObjectInitializerAlignmentTitle), Resources.ResourceManager, typeof(Resources)),
            new LocalizableResourceString(nameof(Resources.ObjectInitializerAlignmentMessageFormat), Resources.ResourceManager, typeof(Resources)),
            Category,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: new LocalizableResourceString(nameof(Resources.ObjectInitializerAlignmentDescription), Resources.ResourceManager, typeof(Resources)),
            customTags: WellKnownDiagnosticTags.Unnecessary);

        // Parameter Alignment Rule
        private static readonly DiagnosticDescriptor ParameterAlignmentRule = new DiagnosticDescriptor(
            ParameterAlignmentId,
            new LocalizableResourceString(nameof(Resources.ParameterAlignmentTitle), Resources.ResourceManager, typeof(Resources)),
            new LocalizableResourceString(nameof(Resources.ParameterAlignmentMessageFormat), Resources.ResourceManager, typeof(Resources)),
            Category,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: new LocalizableResourceString(nameof(Resources.ParameterAlignmentDescription), Resources.ResourceManager, typeof(Resources)),
            customTags: WellKnownDiagnosticTags.Unnecessary);

        // Argument Alignment Rule
        private static readonly DiagnosticDescriptor ArgumentAlignmentRule = new DiagnosticDescriptor(
            ArgumentAlignmentId,
            new LocalizableResourceString(nameof(Resources.ArgumentAlignmentTitle), Resources.ResourceManager, typeof(Resources)),
            new LocalizableResourceString(nameof(Resources.ArgumentAlignmentMessageFormat), Resources.ResourceManager, typeof(Resources)),
            Category,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: new LocalizableResourceString(nameof(Resources.ArgumentAlignmentDescription), Resources.ResourceManager, typeof(Resources)),
            customTags: WellKnownDiagnosticTags.Unnecessary);

        // Chained Method Alignment Rule
        private static readonly DiagnosticDescriptor ChainedMethodAlignmentRule = new DiagnosticDescriptor(
            ChainedMethodAlignmentId,
            new LocalizableResourceString(nameof(Resources.ChainedMethodAlignmentTitle), Resources.ResourceManager, typeof(Resources)),
            new LocalizableResourceString(nameof(Resources.ChainedMethodAlignmentMessageFormat), Resources.ResourceManager, typeof(Resources)),
            Category,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: new LocalizableResourceString(nameof(Resources.ChainedMethodAlignmentDescription), Resources.ResourceManager, typeof(Resources)),
            customTags: WellKnownDiagnosticTags.Unnecessary);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(
                    AssignmentAlignmentRule,
                    ObjectInitializerAlignmentRule,
                    ParameterAlignmentRule,
                    ArgumentAlignmentRule,
                    ChainedMethodAlignmentRule);
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeBlock, SyntaxKind.Block);
            context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeStructDeclaration, SyntaxKind.StructDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeObjectInitializer, SyntaxKind.ObjectInitializerExpression);
            context.RegisterSyntaxNodeAction(AnalyzeParameterList, 
                SyntaxKind.MethodDeclaration, 
                SyntaxKind.ConstructorDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeInvocationExpression, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeBlock(SyntaxNodeAnalysisContext context)
        {
            var block = (BlockSyntax)context.Node;
            var statements = block.Statements.ToList();
            
            if (statements.Count < 2)
                return;

            var maxAlignmentGap = GetMaxAlignmentGapOption(context);

            // Find consecutive assignment groups
            for (int i = 0; i < statements.Count - 1; i++)
            {
                if (!IsAssignmentStatement(statements[i]))
                    continue;

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

                if (group.Count > 1 && !IsAligned(group, maxAlignmentGap))
                {
                    var diagnostic = Diagnostic.Create(
                        AssignmentAlignmentRule,
                        group[0].GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }

                i += group.Count - 1;
            }
        }

        private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;
            AnalyzeMemberFields(context, classDecl.Members.ToList());
        }

        private static void AnalyzeStructDeclaration(SyntaxNodeAnalysisContext context)
        {
            var structDecl = (StructDeclarationSyntax)context.Node;
            AnalyzeMemberFields(context, structDecl.Members.ToList());
        }

        private static void AnalyzeMemberFields(SyntaxNodeAnalysisContext context, List<MemberDeclarationSyntax> members)
        {
            if (members.Count < 2)
                return;

            var maxAlignmentGap = GetMaxAlignmentGapOption(context);

            for (int i = 0; i < members.Count - 1; i++)
            {
                if (!(members[i] is FieldDeclarationSyntax firstField) || !HasInitializer(firstField))
                    continue;

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

                if (group.Count > 1 && !IsFieldGroupAligned(group, maxAlignmentGap))
                {
                    var diagnostic = Diagnostic.Create(
                        AssignmentAlignmentRule,
                        group[0].GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }

                i += group.Count - 1;
            }
        }

        private static void AnalyzeObjectInitializer(SyntaxNodeAnalysisContext context)
        {
            var initializer = (InitializerExpressionSyntax)context.Node;
            
            if (initializer.Expressions.Count < 2)
                return;

            var maxAlignmentGap = GetMaxAlignmentGapOption(context);

            // Check if all expressions are on separate lines
            var expressions = initializer.Expressions.ToList();
            bool allOnSeparateLines = true;
            int? previousLine = null;

            foreach (var expr in expressions)
            {
                var currentLine = GetLineNumber(expr);
                if (previousLine.HasValue && currentLine == previousLine.Value)
                {
                    allOnSeparateLines = false;
                    break;
                }
                previousLine = currentLine;
            }

            if (!allOnSeparateLines)
                return;

            // Check if all are assignments
            if (!expressions.All(e => e is AssignmentExpressionSyntax))
                return;

            if (!IsObjectInitializerAligned(expressions.Cast<AssignmentExpressionSyntax>().ToList(), maxAlignmentGap))
            {
                var diagnostic = Diagnostic.Create(
                    ObjectInitializerAlignmentRule,
                    initializer.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static void AnalyzeParameterList(SyntaxNodeAnalysisContext context)
        {
            ParameterListSyntax parameterList = null;
            int threshold = 0;

            switch (context.Node)
            {
                case MethodDeclarationSyntax methodDecl:
                    parameterList = methodDecl.ParameterList;
                    threshold = GetMethodParameterThresholdOption(context);
                    break;
                case ConstructorDeclarationSyntax ctorDecl:
                    parameterList = ctorDecl.ParameterList;
                    threshold = GetConstructorParameterThresholdOption(context);
                    break;
            }

            if (parameterList == null || parameterList.Parameters.Count < threshold)
                return;

            if (!AreParametersOnSeparateLines(parameterList))
            {
                var diagnostic = Diagnostic.Create(
                    ParameterAlignmentRule,
                    parameterList.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            
            // Check for argument alignment
            if (invocation.ArgumentList != null)
            {
                var argumentThreshold = GetArgumentThresholdOption(context);
                if (invocation.ArgumentList.Arguments.Count >= argumentThreshold)
                {
                    if (!AreArgumentsOnSeparateLines(invocation.ArgumentList))
                    {
                        var diagnostic = Diagnostic.Create(
                            ArgumentAlignmentRule,
                            invocation.ArgumentList.GetLocation());
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }

            // Check for chained method alignment
            if (invocation.Parent is MemberAccessExpressionSyntax)
                return;

            if (!IsInsideMethodBody(invocation))
                return;

            if (invocation.Expression is MemberAccessExpressionSyntax)
            {
                var chainLength = CountMethodCallsInChain(invocation.Expression);
                var minChainLength = GetMinChainLengthOption(context);

                if (chainLength >= minChainLength && !AreMethodCallsOnSeparateLines(invocation.Expression))
                {
                    var diagnostic = Diagnostic.Create(
                        ChainedMethodAlignmentRule,
                        invocation.GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        // Helper methods
        private static bool IsAssignmentStatement(StatementSyntax statement)
        {
            if (statement is LocalDeclarationStatementSyntax localDecl)
                return localDecl.Declaration.Variables.Any(v => v.Initializer != null);

            if (statement is ExpressionStatementSyntax expr)
                return expr.Expression is AssignmentExpressionSyntax;

            return false;
        }

        private static bool HasInitializer(FieldDeclarationSyntax field)
        {
            return field.Declaration.Variables.Any(v => v.Initializer != null);
        }

        private static int GetLineNumber(SyntaxNode node)
        {
            return node.GetLocation().GetLineSpan().StartLinePosition.Line;
        }

        private static bool HasBlankLineBetweenNodes(SyntaxNode previous, SyntaxNode next)
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

        private static bool IsAligned(List<StatementSyntax> statements, int maxGap)
        {
            // Simple check: if trivia is identical, consider it aligned
            // A more sophisticated check would verify actual column positions
            var triviaTexts = statements
                .Select(GetAssignmentTrivia)
                .Where(t => t != null)
                .Distinct()
                .ToList();

            return triviaTexts.Count == 1;
        }

        private static string GetAssignmentTrivia(StatementSyntax statement)
        {
            if (statement is LocalDeclarationStatementSyntax localDecl)
            {
                var firstVar = localDecl.Declaration.Variables.FirstOrDefault();
                if (firstVar?.Initializer != null)
                    return firstVar.Identifier.TrailingTrivia.ToFullString();
            }
            else if (statement is ExpressionStatementSyntax expr &&
                     expr.Expression is AssignmentExpressionSyntax assignment)
            {
                return assignment.Left.GetTrailingTrivia().ToFullString();
            }

            return null;
        }

        private static bool IsFieldGroupAligned(List<FieldDeclarationSyntax> fields, int maxGap)
        {
            var triviaTexts = fields
                .Select(f => f.Declaration.Variables.FirstOrDefault()?.Identifier.TrailingTrivia.ToFullString())
                .Where(t => t != null)
                .Distinct()
                .ToList();

            return triviaTexts.Count == 1;
        }

        private static bool IsObjectInitializerAligned(List<AssignmentExpressionSyntax> assignments, int maxGap)
        {
            var triviaTexts = assignments
                .Select(a => a.Left.GetTrailingTrivia().ToFullString())
                .Distinct()
                .ToList();

            return triviaTexts.Count == 1;
        }

        private static bool AreParametersOnSeparateLines(ParameterListSyntax parameterList)
        {
            int? previousLine = null;
            foreach (var param in parameterList.Parameters)
            {
                var currentLine = GetLineNumber(param);
                if (previousLine.HasValue && currentLine == previousLine.Value)
                    return false;
                previousLine = currentLine;
            }
            return true;
        }

        private static bool AreArgumentsOnSeparateLines(ArgumentListSyntax argumentList)
        {
            int? previousLine = null;
            foreach (var arg in argumentList.Arguments)
            {
                var currentLine = GetLineNumber(arg);
                if (previousLine.HasValue && currentLine == previousLine.Value)
                    return false;
                previousLine = currentLine;
            }
            return true;
        }

        private static int CountMethodCallsInChain(ExpressionSyntax expression)
        {
            int count = 0;
            var current = expression;

            while (current is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Parent is InvocationExpressionSyntax)
                    count++;
                current = memberAccess.Expression;
            }

            return count;
        }

        private static bool AreMethodCallsOnSeparateLines(ExpressionSyntax expression)
        {
            int? previousLine = null;
            var current = expression;

            while (current is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Parent is InvocationExpressionSyntax)
                {
                    var currentLine = GetLineNumber(memberAccess.Name);
                    if (previousLine.HasValue && currentLine == previousLine.Value)
                        return false;
                    previousLine = currentLine;
                }
                current = memberAccess.Expression;
            }

            return true;
        }

        private static bool IsInsideMethodBody(SyntaxNode node)
        {
            var current = node.Parent;
            while (current != null)
            {
                if (current is FieldDeclarationSyntax || current is PropertyDeclarationSyntax)
                    return false;

                if (current is MethodDeclarationSyntax ||
                    current is ConstructorDeclarationSyntax ||
                    current is AccessorDeclarationSyntax ||
                    current is LocalFunctionStatementSyntax ||
                    current is AnonymousFunctionExpressionSyntax)
                    return true;

                if (current is TypeDeclarationSyntax)
                    return false;

                current = current.Parent;
            }

            return false;
        }

        // Configuration options
        private static int GetMaxAlignmentGapOption(SyntaxNodeAnalysisContext context)
        {
            return context.Options.AnalyzerConfigOptionsProvider
                .GetOptions(context.Node.SyntaxTree)
                .TryGetValue("code_align_max_alignment_gap", out var value) && int.TryParse(value, out var gap)
                ? gap
                : 50;
        }

        private static int GetConstructorParameterThresholdOption(SyntaxNodeAnalysisContext context)
        {
            return context.Options.AnalyzerConfigOptionsProvider
                .GetOptions(context.Node.SyntaxTree)
                .TryGetValue("code_align_constructor_parameter_threshold", out var value) && int.TryParse(value, out var threshold)
                ? threshold
                : 3;
        }

        private static int GetMethodParameterThresholdOption(SyntaxNodeAnalysisContext context)
        {
            return context.Options.AnalyzerConfigOptionsProvider
                .GetOptions(context.Node.SyntaxTree)
                .TryGetValue("code_align_method_parameter_threshold", out var value) && int.TryParse(value, out var threshold)
                ? threshold
                : 4;
        }

        private static int GetArgumentThresholdOption(SyntaxNodeAnalysisContext context)
        {
            return context.Options.AnalyzerConfigOptionsProvider
                .GetOptions(context.Node.SyntaxTree)
                .TryGetValue("code_align_argument_threshold", out var value) && int.TryParse(value, out var threshold)
                ? threshold
                : 4;
        }

        private static int GetMinChainLengthOption(SyntaxNodeAnalysisContext context)
        {
            return context.Options.AnalyzerConfigOptionsProvider
                .GetOptions(context.Node.SyntaxTree)
                .TryGetValue("code_align_min_chain_length", out var value) && int.TryParse(value, out var length)
                ? length
                : 2;
        }
    }
}
