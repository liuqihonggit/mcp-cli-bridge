namespace AstCli.Services;

internal sealed class MethodRenameRewriter : CSharpSyntaxRewriter
{
    private readonly string _oldName;
    private readonly string _newName;

    public MethodRenameRewriter(string oldName, string newName)
    {
        _oldName = oldName;
        _newName = newName;
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.Identifier.Text.Equals(_oldName, StringComparison.Ordinal))
        {
            node = node.WithIdentifier(SyntaxFactory.Identifier(_newName));
        }
        return base.VisitMethodDeclaration(node);
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (node.Identifier.Text.Equals(_oldName, StringComparison.Ordinal))
        {
            return node.WithIdentifier(SyntaxFactory.Identifier(_newName));
        }
        return base.VisitIdentifierName(node);
    }
}

internal sealed class AsyncModifierRewriter : CSharpSyntaxRewriter
{
    private readonly string _methodName;

    public AsyncModifierRewriter(string methodName)
    {
        _methodName = methodName;
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.Identifier.Text.Equals(_methodName, StringComparison.Ordinal)
            && !node.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
        {
            var asyncToken = SyntaxFactory.Token(SyntaxKind.AsyncKeyword)
                .WithTrailingTrivia(SyntaxFactory.Space);
            var newModifiers = node.Modifiers.Add(asyncToken);
            node = node.WithModifiers(newModifiers);
        }
        return base.VisitMethodDeclaration(node);
    }
}

internal sealed class ReturnTypeRewriter : CSharpSyntaxRewriter
{
    private readonly string _methodName;

    public ReturnTypeRewriter(string methodName)
    {
        _methodName = methodName;
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (!node.Identifier.Text.Equals(_methodName, StringComparison.Ordinal))
            return base.VisitMethodDeclaration(node);

        var returnType = node.ReturnType;
        if (returnType == null)
            return base.VisitMethodDeclaration(node);

        if (IsTaskType(returnType))
            return base.VisitMethodDeclaration(node);

        TypeSyntax newReturnType;
        if (returnType is PredefinedTypeSyntax predefined
            && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
        {
            newReturnType = SyntaxFactory.IdentifierName("Task")
                .WithTrailingTrivia(SyntaxFactory.Space);
        }
        else
        {
            newReturnType = SyntaxFactory.GenericName(
                SyntaxFactory.Identifier("Task"),
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(returnType.WithoutTrivia())))
                .WithTrailingTrivia(SyntaxFactory.Space);
        }

        return node.WithReturnType(newReturnType);
    }

    private static bool IsTaskType(TypeSyntax type)
    {
        if (type is GenericNameSyntax generic
            && generic.Identifier.Text.Equals("Task", StringComparison.Ordinal))
            return true;

        if (type is IdentifierNameSyntax identifier
            && identifier.Identifier.Text.Equals("Task", StringComparison.Ordinal))
            return true;

        return false;
    }
}

internal sealed class AwaitInvocationRewriter : CSharpSyntaxRewriter
{
    private readonly string _methodName;
    private readonly bool _addConfigureAwait;

    public AwaitInvocationRewriter(string methodName, bool addConfigureAwait)
    {
        _methodName = methodName;
        _addConfigureAwait = addConfigureAwait;
    }

    public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        var updated = TryRewriteInvocation(node.Expression);
        if (updated != null)
            return node.WithExpression(updated);

        return base.VisitExpressionStatement(node);
    }

    public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
    {
        var newDeclarators = new List<VariableDeclaratorSyntax>();
        var changed = false;

        foreach (var decl in node.Declaration.Variables)
        {
            if (decl.Initializer != null)
            {
                var rewritten = TryRewriteInvocation(decl.Initializer.Value);
                if (rewritten != null)
                {
                    newDeclarators.Add(decl.WithInitializer(decl.Initializer.WithValue(rewritten)));
                    changed = true;
                    continue;
                }
            }
            newDeclarators.Add(decl);
        }

        if (changed)
        {
            return node.WithDeclaration(node.Declaration.WithVariables(
                SyntaxFactory.SeparatedList(newDeclarators)));
        }

        return base.VisitLocalDeclarationStatement(node);
    }

    private ExpressionSyntax? TryRewriteInvocation(ExpressionSyntax expression)
    {
        if (expression is not InvocationExpressionSyntax invocation)
            return null;

        if (!IsTargetMethod(invocation))
            return null;

        if (IsAlreadyAwaited(invocation))
            return null;

        var invocationClean = invocation.WithLeadingTrivia();
        var awaitToken = SyntaxFactory.Token(SyntaxKind.AwaitKeyword)
            .WithTrailingTrivia(SyntaxFactory.Space);
        var awaited = SyntaxFactory.AwaitExpression(awaitToken, invocationClean);

        if (_addConfigureAwait)
        {
            var configureAwait = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    awaited,
                    SyntaxFactory.IdentifierName("ConfigureAwait")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(
                            SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression)))));

            return configureAwait;
        }

        return awaited;
    }

    private bool IsTargetMethod(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text.Equals(_methodName, StringComparison.Ordinal),
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text.Equals(_methodName, StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool IsAlreadyAwaited(InvocationExpressionSyntax invocation)
    {
        var parent = invocation.Parent;
        while (parent != null)
        {
            if (parent is AwaitExpressionSyntax)
                return true;
            if (parent is ExpressionSyntax)
                parent = parent.Parent;
            else
                break;
        }
        return false;
    }
}

internal sealed class ParameterAddRewriter : CSharpSyntaxRewriter
{
    private readonly string _methodName;
    private readonly string _paramType;
    private readonly string _paramName;

    public ParameterAddRewriter(string methodName, string paramType, string paramName)
    {
        _methodName = methodName;
        _paramType = paramType;
        _paramName = paramName;
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (!node.Identifier.Text.Equals(_methodName, StringComparison.Ordinal))
            return base.VisitMethodDeclaration(node);

        foreach (var param in node.ParameterList.Parameters)
        {
            if (param.Identifier.Text.Equals(_paramName, StringComparison.Ordinal))
                return base.VisitMethodDeclaration(node);
        }

        var newParam = SyntaxFactory.Parameter(
            SyntaxFactory.List<AttributeListSyntax>(),
            SyntaxFactory.TokenList(),
            SyntaxFactory.IdentifierName(_paramType)
                .WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Identifier(_paramName),
            null);

        var newParams = node.ParameterList.Parameters.Add(newParam);
        var separators = BuildSeparatorsWithTrailingSpace(newParams.Count);

        return node.WithParameterList(node.ParameterList.WithParameters(
            SyntaxFactory.SeparatedList(newParams, separators)));
    }

    private static SyntaxTriviaList TrailingSpace => SyntaxFactory.TriviaList(SyntaxFactory.Space);

    private static SyntaxTokenList BuildSeparatorsWithTrailingSpace(int count)
    {
        var tokens = new SyntaxTokenList();
        for (var i = 0; i < count - 1; i++)
        {
            tokens = tokens.Add(SyntaxFactory.Token(SyntaxKind.CommaToken)
                .WithTrailingTrivia(TrailingSpace));
        }
        return tokens;
    }
}

internal sealed class SyncModifierRemoverRewriter : CSharpSyntaxRewriter
{
    private readonly string _methodName;

    public SyncModifierRemoverRewriter(string methodName)
    {
        _methodName = methodName;
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (!node.Identifier.Text.Equals(_methodName, StringComparison.Ordinal))
            return base.VisitMethodDeclaration(node);

        var asyncIndex = node.Modifiers.IndexOf(SyntaxKind.AsyncKeyword);
        if (asyncIndex < 0)
            return base.VisitMethodDeclaration(node);

        var newModifiers = node.Modifiers.RemoveAt(asyncIndex);
        return node.WithModifiers(newModifiers);
    }
}

internal sealed class SyncReturnTypeRewriter : CSharpSyntaxRewriter
{
    private readonly string _methodName;

    public SyncReturnTypeRewriter(string methodName)
    {
        _methodName = methodName;
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (!node.Identifier.Text.Equals(_methodName, StringComparison.Ordinal))
            return base.VisitMethodDeclaration(node);

        var returnType = node.ReturnType;

        if (returnType is IdentifierNameSyntax id
            && id.Identifier.Text.Equals("Task", StringComparison.Ordinal))
        {
            return node.WithReturnType(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword))
                    .WithTrailingTrivia(SyntaxFactory.Space));
        }

        if (returnType is GenericNameSyntax generic
            && generic.Identifier.Text.Equals("Task", StringComparison.Ordinal)
            && generic.TypeArgumentList.Arguments.Count == 1)
        {
            var innerType = generic.TypeArgumentList.Arguments[0]
                .WithTrailingTrivia(SyntaxFactory.Space);
            return node.WithReturnType(innerType);
        }

        return base.VisitMethodDeclaration(node);
    }
}

internal sealed class SyncAwaitRemoverRewriter : CSharpSyntaxRewriter
{
    private readonly string _methodName;

    public SyncAwaitRemoverRewriter(string methodName)
    {
        _methodName = methodName;
    }

    public override SyntaxNode? VisitAwaitExpression(AwaitExpressionSyntax node)
    {
        var expression = node.Expression;

        if (expression is InvocationExpressionSyntax configureAwaitCall
            && configureAwaitCall.Expression is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Name.Identifier.Text.Equals("ConfigureAwait", StringComparison.Ordinal))
        {
            expression = memberAccess.Expression;
        }

        if (!IsTargetMethod(expression))
            return base.VisitAwaitExpression(node);

        return expression.WithLeadingTrivia(node.GetLeadingTrivia());
    }

    private bool IsTargetMethod(ExpressionSyntax expression)
    {
        return expression switch
        {
            InvocationExpressionSyntax inv => IsTargetInvocation(inv),
            IdentifierNameSyntax id => id.Identifier.Text.Equals(_methodName, StringComparison.Ordinal),
            _ => false
        };
    }

    private bool IsTargetInvocation(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text.Equals(_methodName, StringComparison.Ordinal),
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text.Equals(_methodName, StringComparison.Ordinal),
            _ => false
        };
    }
}

internal sealed class ParameterRemoveRewriter : CSharpSyntaxRewriter
{
    private readonly string _methodName;
    private readonly string _paramName;

    public ParameterRemoveRewriter(string methodName, string paramName)
    {
        _methodName = methodName;
        _paramName = paramName;
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (!node.Identifier.Text.Equals(_methodName, StringComparison.Ordinal))
            return base.VisitMethodDeclaration(node);

        var paramIndex = -1;
        for (var i = 0; i < node.ParameterList.Parameters.Count; i++)
        {
            if (node.ParameterList.Parameters[i].Identifier.Text.Equals(_paramName, StringComparison.Ordinal))
            {
                paramIndex = i;
                break;
            }
        }

        if (paramIndex < 0)
            return base.VisitMethodDeclaration(node);

        var newParams = node.ParameterList.Parameters.RemoveAt(paramIndex);
        var separators = BuildSeparatorsWithTrailingSpace(newParams.Count);

        return node.WithParameterList(node.ParameterList.WithParameters(
            SyntaxFactory.SeparatedList(newParams, separators)));
    }

    private static SyntaxTriviaList TrailingSpace => SyntaxFactory.TriviaList(SyntaxFactory.Space);

    private static SyntaxTokenList BuildSeparatorsWithTrailingSpace(int count)
    {
        var tokens = new SyntaxTokenList();
        for (var i = 0; i < count - 1; i++)
        {
            tokens = tokens.Add(SyntaxFactory.Token(SyntaxKind.CommaToken)
                .WithTrailingTrivia(TrailingSpace));
        }
        return tokens;
    }
}
