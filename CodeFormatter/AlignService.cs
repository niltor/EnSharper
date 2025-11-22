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
        // Default maximum file size to process (1MB) to prevent performance issues
        private const int DefaultMaxFileSizeBytes = 1024 * 1024;

        private readonly int maxFileSizeBytes;
        private readonly int maxAlignmentGap;
        private readonly int constructorParameterThreshold;
        private readonly int methodParameterThreshold;

        /// <summary>
        /// Initializes a new instance of AlignService with default settings
        /// </summary>
        public AlignService() 
            : this(DefaultMaxFileSizeBytes, 50, 3, 4)
        {
        }

        /// <summary>
        /// Initializes a new instance of AlignService with custom settings
        /// </summary>
        /// <param name="maxFileSizeBytes">Maximum file size to process</param>
        /// <param name="maxAlignmentGap">Maximum alignment gap in spaces (0 for unlimited)</param>
        /// <param name="constructorParameterThreshold">Constructor parameter threshold</param>
        /// <param name="methodParameterThreshold">Method parameter threshold</param>
        public AlignService(int maxFileSizeBytes, int maxAlignmentGap, int constructorParameterThreshold, int methodParameterThreshold)
        {
            this.maxFileSizeBytes = maxFileSizeBytes > 0 ? maxFileSizeBytes : DefaultMaxFileSizeBytes;
            this.maxAlignmentGap = maxAlignmentGap >= 0 ? maxAlignmentGap : 0; // 0 means unlimited
            this.constructorParameterThreshold = Math.Max(2, constructorParameterThreshold);
            this.methodParameterThreshold = Math.Max(2, methodParameterThreshold);
        }

        /// <summary>
        /// Formats the given code with alignment
        /// </summary>
        public string FormatCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return code;

            // Check file size limit to prevent performance issues
            if (code.Length > maxFileSizeBytes)
            {
                Logger.LogDebug("AlignService", $"File too large for alignment formatting ({code.Length} bytes, max {maxFileSizeBytes})");
                return code;
            }

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
            var rewriter = new ParameterAlignmentRewriter(constructorParameterThreshold, methodParameterThreshold);
            return rewriter.Visit(root);
        }

        /// <summary>
        /// Aligns assignments: local variables, class fields, and property assignments
        /// </summary>
        private SyntaxNode AlignAssignments(SyntaxNode root)
        {
            var rewriter = new AssignmentAlignmentRewriter(maxAlignmentGap);
            return rewriter.Visit(root);
        }

        /// <summary>
        /// Rewriter for aligning method/constructor parameters
        /// </summary>
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
                // Align parameters if more than threshold
                if (node.ParameterList.Parameters.Count > methodThreshold)
                {
                    var indentation = DetectIndentation(node);
                    var newParameterList = FormatParameterList(node.ParameterList, indentation);
                    node = node.WithParameterList(newParameterList);
                }

                return base.VisitMethodDeclaration(node);
            }

            public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
            {
                // Align parameters if more than threshold
                if (node.ParameterList.Parameters.Count > constructorThreshold)
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
                if (node.ParameterList != null && node.ParameterList.Parameters.Count > constructorThreshold)
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
                if (node.ParameterList != null && node.ParameterList.Parameters.Count > constructorThreshold)
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
                if (node.ParameterList != null && node.ParameterList.Parameters.Count > constructorThreshold)
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
                // by checking if they start on different lines
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
            private readonly int maxAlignmentGap;

            public AssignmentAlignmentRewriter(int maxAlignmentGap)
            {
                this.maxAlignmentGap = maxAlignmentGap;
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
                    // Find consecutive assignment statements of the same type
                    var group = new List<int> { i };
                    
                    if (IsAssignmentStatement(statements[i]))
                    {
                        // Track whether this is a local declaration or expression assignment
                        bool isLocalDeclaration = statements[i] is LocalDeclarationStatementSyntax;
                        
                        while (i + 1 < statements.Count)
                        {
                            int currentLine = GetLineNumber(statements[group.Last()]);
                            int nextLine = GetLineNumber(statements[i + 1]);
                            
                            // Check if next statement is consecutive, is an assignment,
                            // and is the same type (both local decl or both expression assignment)
                            bool nextIsLocalDecl = statements[i + 1] is LocalDeclarationStatementSyntax;
                            if (nextLine - currentLine <= 1 && 
                                IsAssignmentStatement(statements[i + 1]) &&
                                nextIsLocalDecl == isLocalDeclaration)
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
                var fields = indices.Select(idx => allMembers[idx] as FieldDeclarationSyntax)
                    .Where(f => f != null && f.Declaration.Variables.Count > 0)
                    .ToList();

                // If no valid fields after filtering, return empty list
                if (fields.Count == 0)
                    return new List<MemberDeclarationSyntax>();

                // Calculate alignment positions for types
                var typePositions = fields.Select(GetTypeEndPosition).ToList();
                
                // Check if we have any positions to align (guard against empty collections)
                if (typePositions.Count == 0)
                    return new List<MemberDeclarationSyntax>(fields);
                
                var maxTypePos = typePositions.Max();

                // Apply max alignment gap constraint if configured
                if (maxAlignmentGap > 0)
                {
                    var minTypePos = typePositions.Min();
                    if (maxTypePos - minTypePos > maxAlignmentGap)
                    {
                        Logger.LogDebug("AlignService", $"Type alignment gap ({maxTypePos - minTypePos}) exceeds maximum ({maxAlignmentGap}), skipping field group");
                        return new List<MemberDeclarationSyntax>(fields);
                    }
                }

                // Calculate variable positions AFTER type alignment
                // Each variable position needs to account for the aligned type position
                var varPositions = new List<int>();
                for (int i = 0; i < fields.Count; i++)
                {
                    var field = fields[i];
                    var firstVar = field.Declaration.Variables.FirstOrDefault();
                    if (firstVar == null)
                        continue; // Defensive: skip fields with no variables
                    
                    var varName = firstVar.Identifier.Text;
                    // After type alignment, all types end at maxTypePos
                    // Variable starts 1 space after that
                    var varEndPos = maxTypePos + 1 + varName.Length;
                    varPositions.Add(varEndPos);
                }
                
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

                // Filter and validate statements
                var validStatements = new List<StatementSyntax>();
                foreach (var stmt in statements)
                {
                    if (stmt is LocalDeclarationStatementSyntax localDecl)
                    {
                        if (localDecl.Declaration.Variables.Count > 0)
                            validStatements.Add(stmt);
                    }
                    else if (stmt is ExpressionStatementSyntax expr && 
                             expr.Expression is AssignmentExpressionSyntax)
                    {
                        validStatements.Add(stmt);
                    }
                }

                // If no valid statements after filtering, return original
                if (validStatements.Count == 0)
                    return statements;

                // Use validated statements for processing
                statements = validStatements;

                // Calculate alignment positions for types
                var typePositions = statements.Select(GetStatementTypeEndPosition).ToList();
                
                // Check if we have any positions to align (guard against empty collections)
                if (typePositions.Count == 0)
                    return statements;
                
                var maxTypePos = typePositions.Max();

                // Apply max alignment gap constraint if configured
                if (maxAlignmentGap > 0)
                {
                    var minTypePos = typePositions.Min();
                    if (maxTypePos - minTypePos > maxAlignmentGap)
                    {
                        Logger.LogDebug("AlignService", $"Type alignment gap ({maxTypePos - minTypePos}) exceeds maximum ({maxAlignmentGap}), skipping statement group");
                        return statements;
                    }
                }

                // Calculate variable positions AFTER type alignment
                var varPositions = new List<int>();
                for (int i = 0; i < statements.Count; i++)
                {
                    var statement = statements[i];
                    int varEndPos;
                    
                    if (statement is LocalDeclarationStatementSyntax localDecl)
                    {
                        var firstVar = localDecl.Declaration.Variables.First();
                        var varName = firstVar.Identifier.Text;
                        // After type alignment, all types end at maxTypePos
                        // Variable starts 1 space after that
                        varEndPos = maxTypePos + 1 + varName.Length;
                    }
                    else if (statement is ExpressionStatementSyntax expr && 
                             expr.Expression is AssignmentExpressionSyntax assignment)
                    {
                        // For expression assignments (like obj.Prop = value), there's no type
                        // Just use the length of the left side (the variable/property being assigned to)
                        // Note: Due to grouping logic in ProcessStatements, these won't be mixed with local declarations
                        var leftText = assignment.Left.ToString().Trim();
                        varEndPos = leftText.Length;
                    }
                    else
                    {
                        varEndPos = 0;
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
                
                // Calculate where the type ends in the line
                // If there are modifiers, count them (trimmed) + 1 space + type
                // If no modifiers (empty string), just count the type length
                if (string.IsNullOrEmpty(modifiers.Trim()))
                {
                    return typeText.Length;
                }
                else
                {
                    // Modifiers + space between modifiers and type + type
                    return modifiers.TrimEnd().Length + 1 + typeText.Length;
                }
            }

            private int GetStatementTypeEndPosition(StatementSyntax statement)
            {
                if (statement is LocalDeclarationStatementSyntax localDecl)
                {
                    // Local declarations don't have modifiers, just return type length
                    return GetTypeText(localDecl.Declaration.Type).Length;
                }
                return 0;
            }

            private FieldDeclarationSyntax AlignFieldDeclaration(FieldDeclarationSyntax field, int typeSpaces, int varSpaces)
            {
                var newDeclaration = field.Declaration;

                // Add alignment spaces after type + 1 required space before variable name
                // typeSpaces = extra spaces needed for alignment (0 if already aligned)
                // +1 = the one space that must always be present between type and variable
                var newType = field.Declaration.Type.WithTrailingTrivia(
                    SyntaxFactory.Whitespace(new string(' ', Math.Max(0, typeSpaces) + 1))
                );
                newDeclaration = newDeclaration.WithType(newType);

                // Add alignment spaces after variable name + 1 required space before = sign
                // varSpaces = extra spaces needed for alignment (0 if already aligned)
                // +1 = the one space that must always be present between variable and =
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

                    // Add alignment spaces after type + 1 required space before variable name
                    // typeSpaces = extra spaces needed for alignment (0 if already aligned)
                    // +1 = the one space that must always be present between type and variable
                    var newType = localDecl.Declaration.Type.WithTrailingTrivia(
                        SyntaxFactory.Whitespace(new string(' ', Math.Max(0, typeSpaces) + 1))
                    );
                    newDeclaration = newDeclaration.WithType(newType);

                    // Add alignment spaces after variable name + 1 required space before = sign
                    // varSpaces = extra spaces needed for alignment (0 if already aligned)
                    // +1 = the one space that must always be present between variable and =
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
                    // For simple assignments (like aesAlg.Key = ...), add alignment spaces + required space before =
                    // varSpaces = extra spaces needed for alignment (0 if already aligned)
                    // +1 = the one space that must always be present between variable and =
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
