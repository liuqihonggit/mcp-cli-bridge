using static AstCli.Schemas.AstToolSchemaTemplates;

namespace AstCli.Commands;

internal sealed class CommandHandler
{
    public static async Task<OperationResult<JsonElement>> ExecuteAsync(AstCliRequest request)
    {
        return request.Command?.ToLowerInvariant() switch
        {
            "reference_find" => await FindReferencesAsync(request),
            "symbol_query" => await QuerySymbolAsync(request),
            "symbol_rename" => await RenameSymbolAsync(request),
            "symbol_replace" => await ReplaceSymbolAsync(request),
            "symbol_info" => await GetSymbolInfoAsync(request),
            "workspace_overview" => await WorkspaceOverviewAsync(request),
            "file_context" => await FileContextAsync(request),
            "diagnostics" => await DiagnosticsAsync(request),
            "symbol_outline" => await SymbolOutlineAsync(request),
            "list_tools" => ListTools(),
            "list_commands" => ListCommands(),
            _ => Fail($"Unknown command: {request.Command}")
        };
    }

    private static async Task<OperationResult<JsonElement>> QuerySymbolAsync(AstCliRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectPath))
            return Fail("projectPath is required");
        if (string.IsNullOrWhiteSpace(request.SymbolName))
            return Fail("symbolName is required");

#pragma warning disable MCP001
        if (!Directory.Exists(request.ProjectPath))
            return Fail($"Project path not found: {request.ProjectPath}");
#pragma warning restore MCP001

        var result = await AstEngine.QuerySymbolAsync(request.ProjectPath, request.SymbolName, request.Scope);
        return Ok(result, $"Found {result.TotalCount} symbol(s) matching '{request.SymbolName}'", AstCliJsonContext.Default.QuerySymbolResultDto);
    }

    private static async Task<OperationResult<JsonElement>> FindReferencesAsync(AstCliRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectPath))
            return Fail("projectPath is required");
        if (string.IsNullOrWhiteSpace(request.SymbolName))
            return Fail("symbolName is required");

#pragma warning disable MCP001
        if (!Directory.Exists(request.ProjectPath))
            return Fail($"Project path not found: {request.ProjectPath}");
#pragma warning restore MCP001

        var result = await AstEngine.FindReferencesAsync(request.ProjectPath, request.SymbolName);
        return Ok(result, $"Found {result.TotalCount} reference(s) for '{request.SymbolName}'", AstCliJsonContext.Default.FindReferencesResultDto);
    }

    private static async Task<OperationResult<JsonElement>> RenameSymbolAsync(AstCliRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectPath))
            return Fail("projectPath is required");
        if (string.IsNullOrWhiteSpace(request.SymbolName))
            return Fail("symbolName is required");
        if (string.IsNullOrWhiteSpace(request.NewName))
            return Fail("newName is required");

#pragma warning disable MCP001
        if (!Directory.Exists(request.ProjectPath))
            return Fail($"Project path not found: {request.ProjectPath}");
#pragma warning restore MCP001

        var result = await AstEngine.RenameSymbolAsync(request.ProjectPath, request.SymbolName, request.NewName);
        return Ok(result, result.Message, AstCliJsonContext.Default.RenameSymbolResultDto);
    }

    private static async Task<OperationResult<JsonElement>> ReplaceSymbolAsync(AstCliRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectPath))
            return Fail("projectPath is required");
        if (string.IsNullOrWhiteSpace(request.SymbolName))
            return Fail("symbolName (oldName) is required");
        if (string.IsNullOrWhiteSpace(request.NewName))
            return Fail("newName is required");

#pragma warning disable MCP001
        if (!Directory.Exists(request.ProjectPath))
            return Fail($"Project path not found: {request.ProjectPath}");
#pragma warning restore MCP001

        var result = await AstEngine.ReplaceSymbolAsync(request.ProjectPath, request.SymbolName, request.NewName);
        return Ok(result, result.Message, AstCliJsonContext.Default.ReplaceSymbolResultDto);
    }

    private static async Task<OperationResult<JsonElement>> GetSymbolInfoAsync(AstCliRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
            return Fail("filePath is required");

        var projectPath = request.ProjectPath ?? Path.GetDirectoryName(request.FilePath);
        if (projectPath == null)
            return Fail("Invalid project path");

        var result = await AstEngine.GetSymbolInfoAsync(projectPath, request.FilePath, request.LineNumber, request.ColumnNumber);
        return Ok(result, result.Found ? "Symbol found" : "Symbol not found", AstCliJsonContext.Default.GetSymbolInfoResultDto);
    }

    private static async Task<OperationResult<JsonElement>> WorkspaceOverviewAsync(AstCliRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectPath))
            return Fail("projectPath is required");

#pragma warning disable MCP001
        if (!Directory.Exists(request.ProjectPath))
            return Fail($"Project path not found: {request.ProjectPath}");
#pragma warning restore MCP001

        var result = await AstEngine.WorkspaceOverviewAsync(request.ProjectPath);
        return Ok(result, $"Workspace overview: {result.TotalFiles} files, {result.Namespaces.Count} namespaces", AstCliJsonContext.Default.WorkspaceOverviewResultDto);
    }

    private static async Task<OperationResult<JsonElement>> FileContextAsync(AstCliRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectPath))
            return Fail("projectPath is required");
        if (string.IsNullOrWhiteSpace(request.FilePath))
            return Fail("filePath is required");

#pragma warning disable MCP001
        if (!File.Exists(request.FilePath))
            return Fail($"File not found: {request.FilePath}");
#pragma warning restore MCP001

        var result = await AstEngine.FileContextAsync(request.ProjectPath, request.FilePath);
        return Ok(result, $"File context: {result.ProjectUsings.Count} project usings, {result.ReferencedSymbols.Count} referenced symbols", AstCliJsonContext.Default.FileContextResultDto);
    }

    private static async Task<OperationResult<JsonElement>> DiagnosticsAsync(AstCliRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectPath))
            return Fail("projectPath is required");

#pragma warning disable MCP001
        if (!Directory.Exists(request.ProjectPath))
            return Fail($"Project path not found: {request.ProjectPath}");
#pragma warning restore MCP001

        var result = await AstEngine.DiagnosticsAsync(request.ProjectPath, request.FilePath);
        return Ok(result, result.TotalErrorCount > 0 ? $"Found {result.TotalErrorCount} error(s)" : "No errors found", AstCliJsonContext.Default.DiagnosticsResultDto);
    }

    private static async Task<OperationResult<JsonElement>> SymbolOutlineAsync(AstCliRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
            return Fail("filePath is required");

#pragma warning disable MCP001
        if (!File.Exists(request.FilePath))
            return Fail($"File not found: {request.FilePath}");
#pragma warning restore MCP001

        var result = await AstEngine.SymbolOutlineAsync(request.FilePath);
        return Ok(result, $"Symbol outline: {result.Types.Count} type(s)", AstCliJsonContext.Default.SymbolOutlineResultDto);
    }

    private static OperationResult<JsonElement> ListTools()
    {
        var pluginDescriptor = new PluginDescriptor
        {
            Name = "ast",
            Description = "AST CLI - Code analysis, symbol query, find references, and refactoring",
            Category = "code-analysis",
            CommandCount = 5,
            HasDocumentation = false
        };

        return Ok(pluginDescriptor, "", CommonJsonContext.Default.PluginDescriptor);
    }

    private static OperationResult<JsonElement> ListCommands()
    {
        var tools = new List<ToolDefinition>
        {
            new()
            {
                Name = "ast_symbol_query",
                Description = "Query symbols in a C# project by name",
                Category = "code-analysis",
                InputSchema = SymbolQuerySchema()
            },
            new()
            {
                Name = "ast_reference_find",
                Description = "Find all references to a symbol in a C# project",
                Category = "code-analysis",
                InputSchema = ReferenceFindSchema()
            },
            new()
            {
                Name = "ast_symbol_rename",
                Description = "Rename a symbol across all files in a C# project",
                Category = "code-analysis",
                InputSchema = SymbolRenameSchema()
            },
            new()
            {
                Name = "ast_symbol_replace",
                Description = "Replace a symbol name with a new name across all files in a C# project",
                Category = "code-analysis",
                InputSchema = SymbolReplaceSchema()
            },
            new()
            {
                Name = "ast_symbol_info",
                Description = "Get symbol information at a specific position in a file",
                Category = "code-analysis",
                InputSchema = SymbolInfoSchema()
            },
            new()
            {
                Name = "ast_workspace_overview",
                Description = "Get project structure overview: file stats, namespace tree, csproj references, directory roles, entry points",
                Category = "workspace",
                InputSchema = WorkspaceOverviewSchema()
            },
            new()
            {
                Name = "ast_file_context",
                Description = "Analyze file context: usings, project symbol references, same-namespace symbols, reverse dependencies",
                Category = "file-context",
                InputSchema = FileContextSchema()
            },
            new()
            {
                Name = "ast_diagnostics",
                Description = "Get syntax diagnostics for a C# project or specific file",
                Category = "diagnostics",
                InputSchema = DiagnosticsSchema()
            },
            new()
            {
                Name = "ast_symbol_outline",
                Description = "Get symbol outline of a C# file: types, members, line ranges, accessibility",
                Category = "symbol",
                InputSchema = SymbolOutlineSchema()
            }
        };

        return Ok<List<ToolDefinition>>(tools, "", CommonJsonContext.Default.ListToolDefinition);
    }

    private static OperationResult<JsonElement> Fail(string message)
    {
        return new OperationResult<JsonElement>
        {
            Success = false,
            Message = message,
            Data = McpJsonSerializer.EmptyObject
        };
    }

    private static OperationResult<JsonElement> Ok<T>(T data, string message, JsonTypeInfo<T> typeInfo)
    {
        return new OperationResult<JsonElement>
        {
            Success = true,
            Message = message,
            Data = JsonSerializer.SerializeToElement(data, typeInfo)
        };
    }
}
