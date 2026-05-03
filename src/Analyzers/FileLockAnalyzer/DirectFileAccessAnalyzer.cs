using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FileLockAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DirectFileAccessAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCP001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Direct file access detected",
        messageFormat: "Direct file access via '{0}' is not allowed. Use IFileAccessService or FileLockContext.EnterLock() instead.",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "All file read/write operations must go through IFileAccessService to ensure proper locking.");

    private static readonly ImmutableHashSet<string> ForbiddenFileMethods = ImmutableHashSet.Create(
        "ReadAllText",
        "ReadAllTextAsync",
        "WriteAllText",
        "WriteAllTextAsync",
        "ReadAllLines",
        "ReadAllLinesAsync",
        "WriteAllLines",
        "WriteAllLinesAsync",
        "ReadAllBytes",
        "ReadAllBytesAsync",
        "WriteAllBytes",
        "WriteAllBytesAsync",
        "AppendAllText",
        "AppendAllTextAsync",
        "AppendAllLines",
        "AppendAllLinesAsync",
        "AppendText",
        "Create",
        "CreateText",
        "Open",
        "OpenRead",
        "OpenWrite",
        "OpenText",
        "Copy",
        "Move",
        "Delete"
    );

    private static readonly ImmutableHashSet<string> ForbiddenTypes = ImmutableHashSet.Create(
        "global::System.IO.FileStream",
        "global::System.IO.StreamReader",
        "global::System.IO.StreamWriter",
        "global::System.IO.BinaryReader",
        "global::System.IO.BinaryWriter"
    );

    private static readonly ImmutableHashSet<string> LockMethodNames = ImmutableHashSet.Create(
        "EnterLock",
        "DisableEnforcement",
        "AcquireBatchLockAsync"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return;

        var containingType = methodSymbol.ContainingType;
        if (containingType == null)
            return;

        var fullTypeName = containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (fullTypeName == "global::System.IO.File" && ForbiddenFileMethods.Contains(methodSymbol.Name))
        {
            if (IsInLockContext(invocation))
                return;

            var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), $"File.{methodSymbol.Name}");
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

        var symbolInfo = context.SemanticModel.GetSymbolInfo(objectCreation.Type);
        if (symbolInfo.Symbol is not INamedTypeSymbol typeSymbol)
            return;

        var fullTypeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (ForbiddenTypes.Contains(fullTypeName))
        {
            if (IsInLockContext(objectCreation))
                return;

            var shortName = fullTypeName.Replace("global::System.IO.", "");
            var diagnostic = Diagnostic.Create(Rule, objectCreation.GetLocation(), shortName);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsInLockContext(SyntaxNode node)
    {
        var childNode = node;
        var current = node.Parent;

        while (current != null)
        {
            if (current is MethodDeclarationSyntax or AnonymousFunctionExpressionSyntax)
                return false;

            if (current is UsingStatementSyntax usingStatement)
            {
                if (IsLockUsingStatement(usingStatement))
                    return true;
            }

            if (current is BlockSyntax block)
            {
                foreach (var statement in block.Statements)
                {
                    if (statement.SpanStart >= childNode.SpanStart)
                        break;

                    if (statement is LocalDeclarationStatementSyntax localDecl
                        && IsLockUsingDeclaration(localDecl))
                    {
                        return true;
                    }
                }
            }

            childNode = current;
            current = current.Parent;
        }

        return false;
    }

    private static bool IsLockUsingStatement(UsingStatementSyntax usingStatement)
    {
        var expression = usingStatement.Expression;

        if (expression is AwaitExpressionSyntax awaitExpression)
        {
            return IsLockContextMethod(awaitExpression.Expression);
        }

        if (expression is InvocationExpressionSyntax invocation)
        {
            return IsLockContextMethod(invocation);
        }

        var declaration = usingStatement.Declaration;
        if (declaration != null)
        {
            foreach (var variable in declaration.Variables)
            {
                if (variable.Initializer?.Value is AwaitExpressionSyntax awaitInitializer)
                {
                    if (IsLockContextMethod(awaitInitializer.Expression))
                        return true;
                }

                if (variable.Initializer?.Value is InvocationExpressionSyntax varInvocation)
                {
                    if (IsLockContextMethod(varInvocation))
                        return true;
                }
            }
        }

        return false;
    }

    private static bool IsLockUsingDeclaration(LocalDeclarationStatementSyntax localDecl)
    {
        if (!localDecl.UsingKeyword.IsKind(SyntaxKind.UsingKeyword) &&
            !localDecl.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword))
            return false;

        var hasAwaitUsing = localDecl.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword);

        foreach (var variable in localDecl.Declaration.Variables)
        {
            if (variable.Initializer?.Value is AwaitExpressionSyntax awaitExpression)
            {
                if (IsLockContextMethod(awaitExpression.Expression))
                    return true;
            }

            if (variable.Initializer?.Value is InvocationExpressionSyntax invocation)
            {
                if (!hasAwaitUsing && IsLockContextMethod(invocation))
                    return true;
            }
        }

        return false;
    }

    private static bool IsLockContextMethod(ExpressionSyntax expression)
    {
        if (expression is InvocationExpressionSyntax invocation)
        {
            return IsLockContextMethod(invocation);
        }

        return false;
    }

    private static bool IsLockContextMethod(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var methodName = memberAccess.Name.Identifier.Text;

            if (LockMethodNames.Contains(methodName))
            {
                var objectName = GetMemberAccessRootName(memberAccess);

                return methodName switch
                {
                    "EnterLock" or "DisableEnforcement" => objectName == "FileLockContext",
                    "AcquireBatchLockAsync" => true,
                    _ => false
                };
            }
        }

        return false;
    }

    private static string GetMemberAccessRootName(MemberAccessExpressionSyntax memberAccess)
    {
        if (memberAccess.Expression is IdentifierNameSyntax identifier)
        {
            return identifier.Identifier.Text;
        }

        if (memberAccess.Expression is MemberAccessExpressionSyntax nested)
        {
            return nested.Name.Identifier.Text;
        }

        if (memberAccess.Expression is InvocationExpressionSyntax innerInvocation)
        {
            if (innerInvocation.Expression is MemberAccessExpressionSyntax innerMemberAccess)
            {
                return innerMemberAccess.Name.Identifier.Text;
            }
        }

        return string.Empty;
    }
}
