using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeFormatter.Processors
{
    /// <summary>
    /// Aligns method, constructor, and primary constructor parameters that exceed configured thresholds.
    /// </summary>
    internal class ParameterAlignmentProcessor : IAlignmentProcessor
    {
        private readonly int constructorThreshold;
        private readonly int methodThreshold;

        public ParameterAlignmentProcessor(int constructorThreshold, int methodThreshold)
        {
            this.constructorThreshold = constructorThreshold;
            this.methodThreshold = methodThreshold;
        }

        public SyntaxNode Apply(SyntaxNode root)
        {
            var rewriter = new ParameterAlignmentRewriter(constructorThreshold, methodThreshold);
            return rewriter.Visit(root);
        }

        private class ParameterAlignmentRewriter : CSharpSyntaxRewriter
        {
            private readonly int constructorThreshold;
            private readonly int methodThreshold;

            public ParameterAlignmentRewriter(int constructorThreshold, int methodThreshold)
            {
                this.constructorThreshold = constructorThreshold;
                this.methodThreshold = methodThreshold;
            }

            public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                if (node.ParameterList.Parameters.Count >= methodThreshold)
                {
                    var indentation = DetectIndentation(node);
                    var newParameterList = FormatParameterList(node.ParameterList, indentation);
                    node = node.WithParameterList(newParameterList);
                }

                return base.VisitMethodDeclaration(node);
            }

            public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
            {
                if (node.ParameterList.Parameters.Count >= constructorThreshold)
                {
                    var indentation = DetectIndentation(node);
                    var newParameterList = FormatParameterList(node.ParameterList, indentation);
                    node = node.WithParameterList(newParameterList);
                }

                return base.VisitConstructorDeclaration(node);
            }

            public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                if (node.ParameterList != null && node.ParameterList.Parameters.Count >= constructorThreshold)
                {
                    var indentation = DetectIndentation(node);
                    var newParameterList = FormatParameterList(node.ParameterList, indentation);
                    node = node.WithParameterList(newParameterList);
                }

                return base.VisitClassDeclaration(node);
            }

            public override SyntaxNode VisitRecordDeclaration(RecordDeclarationSyntax node)
            {
                if (node.ParameterList != null && node.ParameterList.Parameters.Count >= constructorThreshold)
                {
                    var indentation = DetectIndentation(node);
                    var newParameterList = FormatParameterList(node.ParameterList, indentation);
                    node = node.WithParameterList(newParameterList);
                }

                return base.VisitRecordDeclaration(node);
            }

            public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
            {
                if (node.ParameterList != null && node.ParameterList.Parameters.Count >= constructorThreshold)
                {
                    var indentation = DetectIndentation(node);
                    var newParameterList = FormatParameterList(node.ParameterList, indentation);
                    node = node.WithParameterList(newParameterList);
                }

                return base.VisitStructDeclaration(node);
            }

            private string DetectIndentation(SyntaxNode node)
            {
                var leadingTrivia = node.GetLeadingTrivia();
                foreach (var trivia in leadingTrivia.Reverse())
                {
                    if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                    {
                        var text = trivia.ToFullString();
                        return text + "    ";
                    }
                }

                return "        ";
            }

            private ParameterListSyntax FormatParameterList(ParameterListSyntax parameterList, string indentation)
            {
                if (parameterList.Parameters.Count == 0)
                {
                    return parameterList;
                }

                bool alreadyFormatted = true;
                int? previousLine = null;
                foreach (var param in parameterList.Parameters)
                {
                    var lineSpan = param.GetLocation().GetLineSpan();
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
                    return parameterList;
                }

                var newParameters = SyntaxFactory.SeparatedList<ParameterSyntax>();
                for (int i = 0; i < parameterList.Parameters.Count; i++)
                {
                    var parameter = parameterList.Parameters[i];
                    var leadingTrivia = SyntaxFactory.TriviaList(
                        SyntaxFactory.CarriageReturnLineFeed,
                        SyntaxFactory.Whitespace(indentation)
                    );

                    parameter = parameter.WithLeadingTrivia(leadingTrivia);
                    parameter = parameter.WithTrailingTrivia(SyntaxFactory.TriviaList());
                    newParameters = newParameters.Add(parameter);
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
        }
    }
}
