namespace AstCli.Services;

public sealed class AstEngine
{
    private static readonly string[] s_excludedDirs = ["bin", "obj", ".git", "node_modules", ".vs"];
    private static readonly TimeSpan s_lockTimeout = TimeSpan.FromSeconds(5);

    public static async Task<QuerySymbolResultDto> QuerySymbolAsync(string projectPath, string symbolName, string? scope)
    {
        var files = GetProjectFiles(projectPath, scope);
        var symbols = new List<SymbolInfoDto>();

        foreach (var file in files)
        {
            try
            {
                var content = await ReadFileWithLockAsync(file);
                var tree = CSharpSyntaxTree.ParseText(content, path: file);
                var root = tree.GetCompilationUnitRoot();
                ExtractSymbols(root, file, symbolName, symbols);
            }
            catch
            {
            }
        }

        return new QuerySymbolResultDto
        {
            SymbolName = symbolName,
            Symbols = symbols,
            TotalCount = symbols.Count
        };
    }

    public static async Task<FindReferencesResultDto> FindReferencesAsync(string projectPath, string symbolName)
    {
        var files = GetProjectFiles(projectPath);
        var references = new List<ReferenceLocationDto>();
        SymbolInfoDto? definition = null;

        foreach (var file in files)
        {
            try
            {
                var content = await ReadFileWithLockAsync(file);
                var tree = CSharpSyntaxTree.ParseText(content, path: file);
                var root = tree.GetCompilationUnitRoot();

                FindDefinitionAndReferences(root, file, symbolName, references, ref definition);
            }
            catch
            {
            }
        }

        return new FindReferencesResultDto
        {
            SymbolName = symbolName,
            Symbol = definition,
            References = references,
            TotalCount = references.Count
        };
    }

    public static async Task<RenameSymbolResultDto> RenameSymbolAsync(string projectPath, string symbolName, string newName)
    {
        var files = GetProjectFiles(projectPath);
        var modifiedFiles = new List<string>();

        foreach (var file in files)
        {
            try
            {
                var content = await ReadFileWithLockAsync(file);
                var tree = CSharpSyntaxTree.ParseText(content, path: file);
                var root = tree.GetCompilationUnitRoot();

                var rewriter = new SymbolRenameRewriter(symbolName, newName);
                var newRoot = rewriter.Visit(root);

                if (!newRoot.IsEquivalentTo(root))
                {
                    var newContent = newRoot.ToFullString();
                    await WriteFileWithLockAsync(file, newContent);
                    modifiedFiles.Add(file);
                }
            }
            catch
            {
            }
        }

        return new RenameSymbolResultDto
        {
            OldName = symbolName,
            NewName = newName,
            Success = modifiedFiles.Count > 0,
            ModifiedFiles = modifiedFiles,
            Message = modifiedFiles.Count > 0
                ? $"Renamed '{symbolName}' to '{newName}' in {modifiedFiles.Count} file(s)"
                : $"Symbol '{symbolName}' not found in any file"
        };
    }

    public static async Task<ReplaceSymbolResultDto> ReplaceSymbolAsync(string projectPath, string oldName, string newName)
    {
        var files = GetProjectFiles(projectPath);
        var modifiedFiles = new List<string>();

        foreach (var file in files)
        {
            try
            {
                var content = await ReadFileWithLockAsync(file);
                var tree = CSharpSyntaxTree.ParseText(content, path: file);
                var root = tree.GetCompilationUnitRoot();

                var rewriter = new SymbolRenameRewriter(oldName, newName);
                var newRoot = rewriter.Visit(root);

                if (!newRoot.IsEquivalentTo(root))
                {
                    var newContent = newRoot.ToFullString();
                    await WriteFileWithLockAsync(file, newContent);
                    modifiedFiles.Add(file);
                }
            }
            catch
            {
            }
        }

        return new ReplaceSymbolResultDto
        {
            OldName = oldName,
            NewName = newName,
            Success = modifiedFiles.Count > 0,
            ModifiedFileCount = modifiedFiles.Count,
            ModifiedFiles = modifiedFiles,
            Message = modifiedFiles.Count > 0
                ? $"Replaced '{oldName}' with '{newName}' in {modifiedFiles.Count} file(s)"
                : $"Symbol '{oldName}' not found in any file"
        };
    }

    public static async Task<GetSymbolInfoResultDto> GetSymbolInfoAsync(string projectPath, string filePath, int lineNumber, int columnNumber)
    {
#pragma warning disable MCP001
        if (!File.Exists(filePath))
        {
            return new GetSymbolInfoResultDto { Found = false };
        }
#pragma warning restore MCP001

        try
        {
            var content = await ReadFileWithLockAsync(filePath);
            var tree = CSharpSyntaxTree.ParseText(content, path: filePath);
            var root = tree.GetCompilationUnitRoot();

            var text = root.GetText();
            var absolutePos = text.Lines[lineNumber].Start + columnNumber;
            var textSpan = new TextSpan(absolutePos, 1);

            var node = root.FindNode(textSpan);

            while (node != null)
            {
                switch (node)
                {
                    case ClassDeclarationSyntax classDecl:
                        return CreateSymbolInfoResult(classDecl.Identifier.Text, "Class", classDecl, filePath);
                    case InterfaceDeclarationSyntax ifaceDecl:
                        return CreateSymbolInfoResult(ifaceDecl.Identifier.Text, "Interface", ifaceDecl, filePath);
                    case StructDeclarationSyntax structDecl:
                        return CreateSymbolInfoResult(structDecl.Identifier.Text, "Struct", structDecl, filePath);
                    case EnumDeclarationSyntax enumDecl:
                        return CreateSymbolInfoResult(enumDecl.Identifier.Text, "Enum", enumDecl, filePath);
                    case MethodDeclarationSyntax methodDecl:
                        return CreateSymbolInfoResult(methodDecl.Identifier.Text, "Method", methodDecl, filePath);
                    case PropertyDeclarationSyntax propDecl:
                        return CreateSymbolInfoResult(propDecl.Identifier.Text, "Property", propDecl, filePath);
                    case VariableDeclaratorSyntax varDecl:
                        return CreateSymbolInfoResult(varDecl.Identifier.Text, "Field", varDecl, filePath);
                }

                node = node.Parent;
            }

            return new GetSymbolInfoResultDto { Found = false };
        }
        catch
        {
            return new GetSymbolInfoResultDto { Found = false };
        }
    }

    private static async Task<string> ReadFileWithLockAsync(string filePath)
    {
        var lockResult = await Common.FileLock.FileLockService.AcquireAsync(filePath, s_lockTimeout);
        if (!lockResult.Success || lockResult.Lock == null)
        {
            throw new TimeoutException($"Failed to acquire lock for file: {filePath}");
        }

        await using (lockResult.Lock)
        {
#pragma warning disable MCP001
            return await File.ReadAllTextAsync(filePath);
#pragma warning restore MCP001
        }
    }

    private static async Task WriteFileWithLockAsync(string filePath, string content)
    {
        var lockResult = await Common.FileLock.FileLockService.AcquireAsync(filePath, s_lockTimeout);
        if (!lockResult.Success || lockResult.Lock == null)
        {
            throw new TimeoutException($"Failed to acquire lock for file: {filePath}");
        }

        await using (lockResult.Lock)
        {
#pragma warning disable MCP001
            await File.WriteAllTextAsync(filePath, content);
#pragma warning restore MCP001
        }
    }

    private static GetSymbolInfoResultDto CreateSymbolInfoResult(string name, string kind, SyntaxNode node, string filePath)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        return new GetSymbolInfoResultDto
        {
            Found = true,
            Symbol = new SymbolInfoDto
            {
                Name = name,
                Kind = kind,
                FilePath = filePath,
                Line = lineSpan.StartLinePosition.Line,
                Column = lineSpan.StartLinePosition.Character
            }
        };
    }

    private static void ExtractSymbols(CompilationUnitSyntax root, string filePath, string symbolName, List<SymbolInfoDto> results)
    {
        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case ClassDeclarationSyntax classDecl when Matches(classDecl.Identifier.Text, symbolName):
                    AddSymbol(results, classDecl.Identifier.Text, "Class", classDecl, filePath);
                    break;
                case InterfaceDeclarationSyntax ifaceDecl when Matches(ifaceDecl.Identifier.Text, symbolName):
                    AddSymbol(results, ifaceDecl.Identifier.Text, "Interface", ifaceDecl, filePath);
                    break;
                case StructDeclarationSyntax structDecl when Matches(structDecl.Identifier.Text, symbolName):
                    AddSymbol(results, structDecl.Identifier.Text, "Struct", structDecl, filePath);
                    break;
                case EnumDeclarationSyntax enumDecl when Matches(enumDecl.Identifier.Text, symbolName):
                    AddSymbol(results, enumDecl.Identifier.Text, "Enum", enumDecl, filePath);
                    break;
                case MethodDeclarationSyntax methodDecl when Matches(methodDecl.Identifier.Text, symbolName):
                    AddSymbol(results, methodDecl.Identifier.Text, "Method", methodDecl, filePath);
                    break;
                case PropertyDeclarationSyntax propDecl when Matches(propDecl.Identifier.Text, symbolName):
                    AddSymbol(results, propDecl.Identifier.Text, "Property", propDecl, filePath);
                    break;
                case VariableDeclaratorSyntax varDecl when Matches(varDecl.Identifier.Text, symbolName):
                    AddSymbol(results, varDecl.Identifier.Text, "Field", varDecl, filePath);
                    break;
            }
        }
    }

    private static bool Matches(string actual, string query)
    {
        if (string.IsNullOrEmpty(query) || query == "*")
            return true;

        return actual.Equals(query, StringComparison.OrdinalIgnoreCase)
            || actual.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static void AddSymbol(List<SymbolInfoDto> results, string name, string kind, SyntaxNode node, string filePath)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        var ns = GetContainingNamespace(node);

        results.Add(new SymbolInfoDto
        {
            Name = name,
            Kind = kind,
            ContainingNamespace = ns,
            FilePath = filePath,
            Line = lineSpan.StartLinePosition.Line,
            Column = lineSpan.StartLinePosition.Character
        });
    }

    private static string? GetContainingNamespace(SyntaxNode node)
    {
        var nsDecl = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        return nsDecl?.Name.ToString();
    }

    private static void FindDefinitionAndReferences(
        CompilationUnitSyntax root, string filePath, string symbolName,
        List<ReferenceLocationDto> references, ref SymbolInfoDto? definition)
    {
        foreach (var node in root.DescendantNodes())
        {
            string? identifierText = null;
            bool isDeclaration = false;

            switch (node)
            {
                case ClassDeclarationSyntax classDecl:
                    identifierText = classDecl.Identifier.Text;
                    isDeclaration = true;
                    break;
                case InterfaceDeclarationSyntax ifaceDecl:
                    identifierText = ifaceDecl.Identifier.Text;
                    isDeclaration = true;
                    break;
                case StructDeclarationSyntax structDecl:
                    identifierText = structDecl.Identifier.Text;
                    isDeclaration = true;
                    break;
                case EnumDeclarationSyntax enumDecl:
                    identifierText = enumDecl.Identifier.Text;
                    isDeclaration = true;
                    break;
                case MethodDeclarationSyntax methodDecl:
                    identifierText = methodDecl.Identifier.Text;
                    isDeclaration = true;
                    break;
                case PropertyDeclarationSyntax propDecl:
                    identifierText = propDecl.Identifier.Text;
                    isDeclaration = true;
                    break;
                case VariableDeclaratorSyntax varDecl:
                    identifierText = varDecl.Identifier.Text;
                    isDeclaration = true;
                    break;
                case IdentifierNameSyntax idName:
                    identifierText = idName.Identifier.Text;
                    isDeclaration = false;
                    break;
            }

            if (identifierText == null || !identifierText.Equals(symbolName, StringComparison.OrdinalIgnoreCase))
                continue;

            var lineSpan = node.GetLocation().GetLineSpan();
            var line = lineSpan.StartLinePosition.Line;
            var col = lineSpan.StartLinePosition.Character;

            var context = GetLineContent(filePath, line);

            if (isDeclaration && definition == null)
            {
                definition = new SymbolInfoDto
                {
                    Name = identifierText,
                    Kind = GetNodeKind(node),
                    FilePath = filePath,
                    Line = line,
                    Column = col
                };
            }

            references.Add(new ReferenceLocationDto
            {
                FilePath = filePath,
                Line = line,
                Column = col,
                SymbolName = identifierText,
                Kind = GetNodeKind(node),
                Context = context
            });
        }
    }

    private static string? GetLineContent(string filePath, int lineIndex)
    {
        try
        {
#pragma warning disable MCP001
            var lines = File.ReadLines(filePath);
#pragma warning restore MCP001
            var i = 0;
            foreach (var line in lines)
            {
                if (i == lineIndex) return line.Trim();
                i++;
            }
        }
        catch
        {
        }
        return null;
    }

    private static string GetNodeKind(SyntaxNode node)
    {
        return node switch
        {
            ClassDeclarationSyntax => "Class",
            InterfaceDeclarationSyntax => "Interface",
            StructDeclarationSyntax => "Struct",
            EnumDeclarationSyntax => "Enum",
            MethodDeclarationSyntax => "Method",
            PropertyDeclarationSyntax => "Property",
            VariableDeclaratorSyntax => "Field",
            IdentifierNameSyntax => "Reference",
            _ => "Unknown"
        };
    }

    private static List<string> GetProjectFiles(string projectPath, string? scope = null)
    {
#pragma warning disable MCP001
        if (!Directory.Exists(projectPath))
            return [];
#pragma warning restore MCP001

        var searchOption = scope?.Equals("file", StringComparison.OrdinalIgnoreCase) == true
            ? SearchOption.TopDirectoryOnly
            : SearchOption.AllDirectories;

#pragma warning disable MCP001
        return Directory.GetFiles(projectPath, "*.cs", searchOption)
#pragma warning restore MCP001
            .Where(f => !IsExcluded(f))
            .ToList();
    }

    private static bool IsExcluded(string filePath)
    {
        foreach (var dir in s_excludedDirs)
        {
            if (filePath.Contains($"\\{dir}\\", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains($"/{dir}/", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

file sealed class SymbolRenameRewriter : CSharpSyntaxRewriter
{
    private readonly string _oldName;
    private readonly string _newName;

    public SymbolRenameRewriter(string oldName, string newName)
    {
        _oldName = oldName;
        _newName = newName;
    }

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        if (node.Identifier.Text.Equals(_oldName, StringComparison.Ordinal))
        {
            node = node.WithIdentifier(SyntaxFactory.Identifier(_newName));
        }
        return base.VisitClassDeclaration(node);
    }

    public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        if (node.Identifier.Text.Equals(_oldName, StringComparison.Ordinal))
        {
            node = node.WithIdentifier(SyntaxFactory.Identifier(_newName));
        }
        return base.VisitInterfaceDeclaration(node);
    }

    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
    {
        if (node.Identifier.Text.Equals(_oldName, StringComparison.Ordinal))
        {
            node = node.WithIdentifier(SyntaxFactory.Identifier(_newName));
        }
        return base.VisitStructDeclaration(node);
    }

    public override SyntaxNode? VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        if (node.Identifier.Text.Equals(_oldName, StringComparison.Ordinal))
        {
            node = node.WithIdentifier(SyntaxFactory.Identifier(_newName));
        }
        return base.VisitEnumDeclaration(node);
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.Identifier.Text.Equals(_oldName, StringComparison.Ordinal))
        {
            node = node.WithIdentifier(SyntaxFactory.Identifier(_newName));
        }
        return base.VisitMethodDeclaration(node);
    }

    public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        if (node.Identifier.Text.Equals(_oldName, StringComparison.Ordinal))
        {
            node = node.WithIdentifier(SyntaxFactory.Identifier(_newName));
        }
        return base.VisitPropertyDeclaration(node);
    }

    public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
    {
        if (node.Identifier.Text.Equals(_oldName, StringComparison.Ordinal))
        {
            node = node.WithIdentifier(SyntaxFactory.Identifier(_newName));
        }
        return base.VisitVariableDeclarator(node);
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
