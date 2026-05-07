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

    public static async Task<WorkspaceOverviewResultDto> WorkspaceOverviewAsync(string projectPath)
    {
#pragma warning disable MCP001
        if (!Directory.Exists(projectPath))
            return new WorkspaceOverviewResultDto { ProjectPath = projectPath };
#pragma warning restore MCP001

        var files = GetProjectFiles(projectPath);
        var namespaces = new HashSet<string>();
        var csprojFiles = new List<CsprojInfoDto>();
        var directoryRoles = new List<DirectoryRoleDto>();
        var entryPoints = new List<string>();
        var totalLines = 0;

        foreach (var file in files)
        {
            try
            {
                var content = await ReadFileWithLockAsync(file);
                totalLines += content.Split('\n').Length;

                var tree = CSharpSyntaxTree.ParseText(content, path: file);
                var root = tree.GetCompilationUnitRoot();

                foreach (var ns in root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
                {
                    namespaces.Add(ns.Name.ToString());
                }

                if (HasEntryPoint(root))
                {
                    entryPoints.Add(file);
                }
            }
            catch
            {
            }
        }

#pragma warning disable MCP001
        foreach (var csproj in Directory.GetFiles(projectPath, "*.csproj", SearchOption.AllDirectories))
#pragma warning restore MCP001
        {
            try
            {
                var content = await ReadFileWithLockAsync(csproj);
                var refs = new List<string>();
                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(content, @"<ProjectReference\s+Include=""([^""]+)"""))
                {
                    refs.Add(match.Groups[1].Value);
                }
                csprojFiles.Add(new CsprojInfoDto { FilePath = csproj, ProjectReferences = refs });
            }
            catch
            {
            }
        }

#pragma warning disable MCP001
        foreach (var dir in Directory.GetDirectories(projectPath, "*", SearchOption.AllDirectories))
#pragma warning restore MCP001
        {
            var dirName = System.IO.Path.GetFileName(dir);
            if (IsExcluded(dir)) continue;

            var role = InferDirectoryRole(dirName);
            if (role != null)
            {
                directoryRoles.Add(new DirectoryRoleDto { Path = dir, Role = role });
            }
        }

        return new WorkspaceOverviewResultDto
        {
            ProjectPath = projectPath,
            TotalFiles = files.Count,
            TotalLines = totalLines,
            Namespaces = namespaces.OrderBy(n => n).ToList(),
            CsprojFiles = csprojFiles,
            DirectoryRoles = directoryRoles,
            EntryPoints = entryPoints
        };
    }

    public static async Task<FileContextResultDto> FileContextAsync(string projectPath, string filePath)
    {
#pragma warning disable MCP001
        if (!File.Exists(filePath))
            return new FileContextResultDto { FilePath = filePath };
#pragma warning restore MCP001

        var content = await ReadFileWithLockAsync(filePath);
        var tree = CSharpSyntaxTree.ParseText(content, path: filePath);
        var root = tree.GetCompilationUnitRoot();

        var systemUsings = new List<string>();
        var projectUsings = new List<string>();

        foreach (var usingDir in root.Usings)
        {
            var nsName = usingDir.Name?.ToString() ?? "";
            if (string.IsNullOrEmpty(nsName)) continue;

            if (IsSystemNamespace(nsName))
                systemUsings.Add(nsName);
            else
                projectUsings.Add(nsName);
        }

        var fileNamespace = root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault()?.Name.ToString();

        var identifiersInFile = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Select(id => id.Identifier.Text)
            .Distinct()
            .ToHashSet();

        var referencedSymbols = new List<SymbolInfoDto>();
        var sameNamespaceSymbols = new List<SymbolInfoDto>();
        var reverseDependencies = new List<string>();

        var allProjectFiles = GetProjectFiles(projectPath);

        foreach (var otherFile in allProjectFiles)
        {
            try
            {
                var otherContent = await ReadFileWithLockAsync(otherFile);
                var otherTree = CSharpSyntaxTree.ParseText(otherContent, path: otherFile);
                var otherRoot = otherTree.GetCompilationUnitRoot();

                var otherNamespace = otherRoot.DescendantNodes()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .FirstOrDefault()?.Name.ToString();

                if (otherNamespace == fileNamespace && otherFile != filePath)
                {
                    ExtractPublicSymbols(otherRoot, otherFile, sameNamespaceSymbols);
                }

                if (otherFile != filePath)
                {
                    var otherIdentifiers = otherRoot.DescendantNodes()
                        .OfType<IdentifierNameSyntax>()
                        .Select(id => id.Identifier.Text)
                        .ToHashSet();

                    var fileDeclaredNames = root.DescendantNodes()
                        .Where(n => n is ClassDeclarationSyntax || n is InterfaceDeclarationSyntax || n is EnumDeclarationSyntax || n is StructDeclarationSyntax || n is MethodDeclarationSyntax || n is PropertyDeclarationSyntax)
                        .Select(n => GetDeclaredName(n))
                        .Where(name => name != null)
                        .ToHashSet();

                    if (fileDeclaredNames.Overlaps(otherIdentifiers))
                    {
                        reverseDependencies.Add(otherFile);
                    }
                }
            }
            catch
            {
            }
        }

        foreach (var projectUsing in projectUsings)
        {
            foreach (var otherFile in allProjectFiles)
            {
                try
                {
                    var otherContent = await ReadFileWithLockAsync(otherFile);
                    var otherTree = CSharpSyntaxTree.ParseText(otherContent, path: otherFile);
                    var otherRoot = otherTree.GetCompilationUnitRoot();

                    var otherNamespace = otherRoot.DescendantNodes()
                        .OfType<BaseNamespaceDeclarationSyntax>()
                        .FirstOrDefault()?.Name.ToString();

                    if (otherNamespace == projectUsing)
                    {
                        var publicSymbols = new List<SymbolInfoDto>();
                        ExtractPublicSymbols(otherRoot, otherFile, publicSymbols);

                        foreach (var sym in publicSymbols)
                        {
                            if (identifiersInFile.Contains(sym.Name))
                            {
                                referencedSymbols.Add(sym);
                            }
                        }
                    }
                }
                catch
                {
                }
            }
        }

        return new FileContextResultDto
        {
            FilePath = filePath,
            SystemUsings = systemUsings,
            ProjectUsings = projectUsings,
            ReferencedSymbols = referencedSymbols,
            SameNamespaceSymbols = sameNamespaceSymbols,
            ReverseDependencies = reverseDependencies
        };
    }

    public static async Task<DiagnosticsResultDto> DiagnosticsAsync(string projectPath, string? filePath)
    {
        var files = filePath != null
            ? new List<string> { filePath }
            : GetProjectFiles(projectPath);

        var errors = new List<DiagnosticItemDto>();
        var errorCount = 0;
        var warningCount = 0;

        foreach (var file in files)
        {
#pragma warning disable MCP001
            if (!File.Exists(file)) continue;
#pragma warning restore MCP001

            try
            {
                var content = await ReadFileWithLockAsync(file);
                var tree = CSharpSyntaxTree.ParseText(content, path: file);
                var diagnostics = tree.GetDiagnostics();

                foreach (var diag in diagnostics)
                {
                    var lineSpan = diag.Location.GetLineSpan();
                    var severity = diag.Severity.ToString();

                    if (diag.Severity == DiagnosticSeverity.Error) errorCount++;
                    else if (diag.Severity == DiagnosticSeverity.Warning) warningCount++;

                    errors.Add(new DiagnosticItemDto
                    {
                        FilePath = file,
                        Line = lineSpan.StartLinePosition.Line,
                        Column = lineSpan.StartLinePosition.Character,
                        Severity = severity,
                        Code = diag.Id,
                        Message = diag.GetMessage()
                    });
                }
            }
            catch
            {
            }
        }

        return new DiagnosticsResultDto
        {
            ProjectPath = projectPath,
            Errors = errors,
            TotalErrorCount = errorCount,
            TotalWarningCount = warningCount
        };
    }

    public static async Task<SymbolOutlineResultDto> SymbolOutlineAsync(string filePath)
    {
#pragma warning disable MCP001
        if (!File.Exists(filePath))
            return new SymbolOutlineResultDto { FilePath = filePath };
#pragma warning restore MCP001

        var content = await ReadFileWithLockAsync(filePath);
        var tree = CSharpSyntaxTree.ParseText(content, path: filePath);
        var root = tree.GetCompilationUnitRoot();

        var types = new List<TypeOutlineDto>();

        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var typeOutline = new TypeOutlineDto
            {
                Name = typeDecl.Identifier.Text,
                Kind = GetTypeKind(typeDecl),
                Accessibility = GetAccessibility(typeDecl.Modifiers),
                StartLine = typeDecl.GetLocation().GetLineSpan().StartLinePosition.Line,
                EndLine = typeDecl.GetLocation().GetLineSpan().EndLinePosition.Line,
                Members = []
            };

            foreach (var member in typeDecl.Members)
            {
                typeOutline.Members.Add(new MemberOutlineDto
                {
                    Name = GetMemberName(member),
                    Kind = GetMemberKind(member),
                    Accessibility = GetAccessibility(member.Modifiers),
                    Line = member.GetLocation().GetLineSpan().StartLinePosition.Line
                });
            }

            types.Add(typeOutline);
        }

        foreach (var enumDecl in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
        {
            var typeOutline = new TypeOutlineDto
            {
                Name = enumDecl.Identifier.Text,
                Kind = "Enum",
                Accessibility = GetAccessibility(enumDecl.Modifiers),
                StartLine = enumDecl.GetLocation().GetLineSpan().StartLinePosition.Line,
                EndLine = enumDecl.GetLocation().GetLineSpan().EndLinePosition.Line,
                Members = []
            };

            foreach (var member in enumDecl.Members)
            {
                typeOutline.Members.Add(new MemberOutlineDto
                {
                    Name = member.Identifier.Text,
                    Kind = "EnumMember",
                    Accessibility = "",
                    Line = member.GetLocation().GetLineSpan().StartLinePosition.Line
                });
            }

            types.Add(typeOutline);
        }

        return new SymbolOutlineResultDto
        {
            FilePath = filePath,
            Types = types.OrderBy(t => t.StartLine).ToList()
        };
    }

    private static bool HasEntryPoint(CompilationUnitSyntax root)
    {
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (method.Identifier.Text == "Main")
            {
                var returnType = method.ReturnType?.ToString();
                if (returnType == "void" || returnType == "Task" || returnType == "int" || returnType == "Task<int>")
                    return true;
            }
        }

        if (root.Members.Count > 0 && root.Usings.Count > 0 && !root.DescendantNodes().OfType<TypeDeclarationSyntax>().Any())
            return true;

        return false;
    }

    private static bool IsSystemNamespace(string ns)
    {
        return ns.StartsWith("System", StringComparison.Ordinal)
            || ns.StartsWith("Microsoft", StringComparison.Ordinal)
            || ns.StartsWith("Net", StringComparison.Ordinal);
    }

    private static void ExtractPublicSymbols(CompilationUnitSyntax root, string filePath, List<SymbolInfoDto> results)
    {
        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case ClassDeclarationSyntax classDecl when IsPublic(classDecl.Modifiers):
                    AddSymbol(results, classDecl.Identifier.Text, "Class", classDecl, filePath);
                    break;
                case InterfaceDeclarationSyntax ifaceDecl when IsPublic(ifaceDecl.Modifiers):
                    AddSymbol(results, ifaceDecl.Identifier.Text, "Interface", ifaceDecl, filePath);
                    break;
                case StructDeclarationSyntax structDecl when IsPublic(structDecl.Modifiers):
                    AddSymbol(results, structDecl.Identifier.Text, "Struct", structDecl, filePath);
                    break;
                case EnumDeclarationSyntax enumDecl when IsPublic(enumDecl.Modifiers):
                    AddSymbol(results, enumDecl.Identifier.Text, "Enum", enumDecl, filePath);
                    break;
                case MethodDeclarationSyntax methodDecl when IsPublic(methodDecl.Modifiers):
                    AddSymbol(results, methodDecl.Identifier.Text, "Method", methodDecl, filePath);
                    break;
                case PropertyDeclarationSyntax propDecl when IsPublic(propDecl.Modifiers):
                    AddSymbol(results, propDecl.Identifier.Text, "Property", propDecl, filePath);
                    break;
            }
        }
    }

    private static bool IsPublic(SyntaxTokenList modifiers)
    {
        return modifiers.Any(m => m.Text == "public");
    }

    private static string? GetDeclaredName(SyntaxNode node)
    {
        return node switch
        {
            ClassDeclarationSyntax c => c.Identifier.Text,
            InterfaceDeclarationSyntax i => i.Identifier.Text,
            EnumDeclarationSyntax e => e.Identifier.Text,
            StructDeclarationSyntax s => s.Identifier.Text,
            MethodDeclarationSyntax m => m.Identifier.Text,
            PropertyDeclarationSyntax p => p.Identifier.Text,
            _ => null
        };
    }

    private static string GetTypeKind(TypeDeclarationSyntax typeDecl)
    {
        return typeDecl.Keyword.Text switch
        {
            "class" => "Class",
            "interface" => "Interface",
            "struct" => "Struct",
            "record" => "Record",
            _ => "Type"
        };
    }

    private static string GetAccessibility(SyntaxTokenList modifiers)
    {
        foreach (var mod in modifiers)
        {
            if (mod.Text == "public") return "public";
            if (mod.Text == "internal") return "internal";
            if (mod.Text == "private") return "private";
            if (mod.Text == "protected") return "protected";
        }
        return "private";
    }

    private static string GetMemberName(MemberDeclarationSyntax member)
    {
        return member switch
        {
            MethodDeclarationSyntax m => m.Identifier.Text,
            PropertyDeclarationSyntax p => p.Identifier.Text,
            FieldDeclarationSyntax f => f.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "",
            EventDeclarationSyntax e => e.Identifier.Text,
            ConstructorDeclarationSyntax => ".ctor",
            _ => ""
        };
    }

    private static string GetMemberKind(MemberDeclarationSyntax member)
    {
        return member switch
        {
            MethodDeclarationSyntax => "Method",
            PropertyDeclarationSyntax => "Property",
            FieldDeclarationSyntax => "Field",
            EventDeclarationSyntax => "Event",
            ConstructorDeclarationSyntax => "Constructor",
            _ => "Member"
        };
    }

    private static string? InferDirectoryRole(string dirName)
    {
        return dirName.ToLowerInvariant() switch
        {
            "models" or "model" => "Models",
            "services" or "service" => "Services",
            "controllers" or "controller" => "Controllers",
            "views" or "view" => "Views",
            "repositories" or "repository" => "Repositories",
            "dto" or "dtos" => "DTOs",
            "commands" or "command" => "Commands",
            "queries" or "query" => "Queries",
            "handlers" or "handler" => "Handlers",
            "middleware" => "Middleware",
            "extensions" or "extension" => "Extensions",
            "helpers" or "helper" => "Helpers",
            "utils" or "utilities" => "Utilities",
            "config" or "configuration" => "Configuration",
            "schemas" or "schema" => "Schemas",
            "validators" or "validator" => "Validators",
            "interfaces" or "interface" => "Interfaces",
            "enums" or "enum" => "Enums",
            "exceptions" or "exception" => "Exceptions",
            "constants" or "constant" => "Constants",
            _ => null
        };
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
