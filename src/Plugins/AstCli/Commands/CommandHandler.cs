using Common.Contracts.Attributes;
using Common.Contracts.Schema;
using AstCli.Schemas;

namespace AstCli.Commands;

[CliCommandHandler("ast_cli", "AST CLI - Code analysis, symbol query, find references, and refactoring", Category = "code-analysis", ToolNamePrefix = "ast_", HasDocumentation = true)]
internal sealed partial class CommandHandler
{
    [CliCommand("symbol_query", Description = "Query symbols in a project by name", Category = "code-analysis", SchemaType = typeof(AstSchemas.SymbolQuery))]
    private static async Task<OperationResult<JsonElement>> QuerySymbolAsync(AstCliRequest request)
    {
        var languageError = ValidateLanguage(request.Language);
        if (languageError != null) return languageError;

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

    [CliCommand("reference_find", Description = "Find all references to a symbol in a project", Category = "code-analysis", SchemaType = typeof(AstSchemas.ReferenceFind))]
    private static async Task<OperationResult<JsonElement>> FindReferencesAsync(AstCliRequest request)
    {
        var languageError = ValidateLanguage(request.Language);
        if (languageError != null) return languageError;

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

    [CliCommand("symbol_rename", Description = "Rename a symbol across all files in a project", Category = "code-analysis", SchemaType = typeof(AstSchemas.SymbolRename))]
    private static async Task<OperationResult<JsonElement>> RenameSymbolAsync(AstCliRequest request)
    {
        var languageError = ValidateLanguage(request.Language);
        if (languageError != null) return languageError;

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

    [CliCommand("symbol_replace", Description = "Replace a symbol name with a new name across all files in a project", Category = "code-analysis", SchemaType = typeof(AstSchemas.SymbolReplace))]
    private static async Task<OperationResult<JsonElement>> ReplaceSymbolAsync(AstCliRequest request)
    {
        var languageError = ValidateLanguage(request.Language);
        if (languageError != null) return languageError;

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

    [CliCommand("symbol_info", Description = "Get symbol information at a specific position in a file", Category = "code-analysis", SchemaType = typeof(AstSchemas.SymbolInfo))]
    private static async Task<OperationResult<JsonElement>> GetSymbolInfoAsync(AstCliRequest request)
    {
        var languageError = ValidateLanguage(request.Language);
        if (languageError != null) return languageError;

        if (string.IsNullOrWhiteSpace(request.FilePath))
            return Fail("filePath is required");

        var projectPath = request.ProjectPath ?? Path.GetDirectoryName(request.FilePath);
        if (projectPath == null)
            return Fail("Invalid project path");

        var result = await AstEngine.GetSymbolInfoAsync(projectPath, request.FilePath, request.LineNumber, request.ColumnNumber);
        return Ok(result, result.Found ? "Symbol found" : "Symbol not found", AstCliJsonContext.Default.GetSymbolInfoResultDto);
    }

    [CliCommand("workspace_overview", Description = "Get project structure overview: file stats, namespace tree, csproj references, directory roles, entry points", Category = "workspace", SchemaType = typeof(AstSchemas.WorkspaceOverview))]
    private static async Task<OperationResult<JsonElement>> WorkspaceOverviewAsync(AstCliRequest request)
    {
        var languageError = ValidateLanguage(request.Language);
        if (languageError != null) return languageError;

        if (string.IsNullOrWhiteSpace(request.ProjectPath))
            return Fail("projectPath is required");

#pragma warning disable MCP001
        if (!Directory.Exists(request.ProjectPath))
            return Fail($"Project path not found: {request.ProjectPath}");
#pragma warning restore MCP001

        var result = await AstEngine.WorkspaceOverviewAsync(request.ProjectPath);
        return Ok(result, $"Workspace overview: {result.TotalFiles} files, {result.Namespaces.Count} namespaces", AstCliJsonContext.Default.WorkspaceOverviewResultDto);
    }

    [CliCommand("file_context", Description = "Analyze file context: usings, project symbol references, same-namespace symbols, reverse dependencies", Category = "file-context", SchemaType = typeof(AstSchemas.FileContext))]
    private static async Task<OperationResult<JsonElement>> FileContextAsync(AstCliRequest request)
    {
        var languageError = ValidateLanguage(request.Language);
        if (languageError != null) return languageError;

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

    [CliCommand("diagnostics", Description = "Get syntax diagnostics for a project or specific file", Category = "diagnostics", SchemaType = typeof(AstSchemas.Diagnostics))]
    private static async Task<OperationResult<JsonElement>> DiagnosticsAsync(AstCliRequest request)
    {
        var languageError = ValidateLanguage(request.Language);
        if (languageError != null) return languageError;

        if (string.IsNullOrWhiteSpace(request.ProjectPath))
            return Fail("projectPath is required");

#pragma warning disable MCP001
        if (!Directory.Exists(request.ProjectPath))
            return Fail($"Project path not found: {request.ProjectPath}");
#pragma warning restore MCP001

        var result = await AstEngine.DiagnosticsAsync(request.ProjectPath, request.FilePath);
        return Ok(result, result.TotalErrorCount > 0 ? $"Found {result.TotalErrorCount} error(s)" : "No errors found", AstCliJsonContext.Default.DiagnosticsResultDto);
    }

    [CliCommand("symbol_outline", Description = "Get symbol outline of a file: types, members, line ranges, accessibility", Category = "symbol", SchemaType = typeof(AstSchemas.SymbolOutline))]
    private static async Task<OperationResult<JsonElement>> SymbolOutlineAsync(AstCliRequest request)
    {
        var languageError = ValidateLanguage(request.Language);
        if (languageError != null) return languageError;

        if (string.IsNullOrWhiteSpace(request.FilePath))
            return Fail("filePath is required");

#pragma warning disable MCP001
        if (!File.Exists(request.FilePath))
            return Fail($"File not found: {request.FilePath}");
#pragma warning restore MCP001

        var result = await AstEngine.SymbolOutlineAsync(request.FilePath);
        return Ok(result, $"Symbol outline: {result.Types.Count} type(s)", AstCliJsonContext.Default.SymbolOutlineResultDto);
    }

    [CliCommand("string_query", Description = "Query string literals in a project, optionally filter by prefix or content", Category = "string", SchemaType = typeof(AstSchemas.StringQuery))]
    private static async Task<OperationResult<JsonElement>> StringQueryAsync(AstCliRequest request)
    {
        var languageError = ValidateLanguage(request.Language);
        if (languageError != null) return languageError;

        if (string.IsNullOrWhiteSpace(request.ProjectPath))
            return Fail("projectPath is required");

#pragma warning disable MCP001
        if (!Directory.Exists(request.ProjectPath))
            return Fail($"Project path not found: {request.ProjectPath}");
#pragma warning restore MCP001

        var result = await StringLiteralEngine.QueryAsync(request.ProjectPath, request.FilePath, request.Prefix, request.Filter);
        return Ok(result, $"Found {result.TotalCount} string literal(s)", AstCliJsonContext.Default.StringQueryResultDto);
    }

    [CliCommand("string_prefix", Description = "Insert text at the beginning (position 0) of each string literal", Category = "string", SchemaType = typeof(AstSchemas.StringPrefix))]
    private static async Task<OperationResult<JsonElement>> StringPrefixAsync(AstCliRequest request)
    {
        var languageError = ValidateLanguage(request.Language);
        if (languageError != null) return languageError;

        if (string.IsNullOrWhiteSpace(request.ProjectPath))
            return Fail("projectPath is required");
        if (string.IsNullOrWhiteSpace(request.InsertText))
            return Fail("insertText is required");

#pragma warning disable MCP001
        if (!Directory.Exists(request.ProjectPath))
            return Fail($"Project path not found: {request.ProjectPath}");
#pragma warning restore MCP001

        var result = await StringLiteralEngine.InsertAsync(
            request.ProjectPath, request.FilePath, request.InsertText, 0, "prefix", request.Filter, request.DryRun);
        return Ok(result, result.Message, AstCliJsonContext.Default.StringInsertResultDto);
    }

    [CliCommand("string_suffix", Description = "Insert text at the end of each string literal", Category = "string", SchemaType = typeof(AstSchemas.StringSuffix))]
    private static async Task<OperationResult<JsonElement>> StringSuffixAsync(AstCliRequest request)
    {
        var languageError = ValidateLanguage(request.Language);
        if (languageError != null) return languageError;

        if (string.IsNullOrWhiteSpace(request.ProjectPath))
            return Fail("projectPath is required");
        if (string.IsNullOrWhiteSpace(request.InsertText))
            return Fail("insertText is required");

#pragma warning disable MCP001
        if (!Directory.Exists(request.ProjectPath))
            return Fail($"Project path not found: {request.ProjectPath}");
#pragma warning restore MCP001

        var result = await StringLiteralEngine.InsertAsync(
            request.ProjectPath, request.FilePath, request.InsertText, int.MaxValue, "suffix", request.Filter, request.DryRun);
        return Ok(result, result.Message, AstCliJsonContext.Default.StringInsertResultDto);
    }

    [CliCommand("string_insert", Description = "Insert text at an arbitrary position within each string literal", Category = "string", SchemaType = typeof(AstSchemas.StringInsert))]
    private static async Task<OperationResult<JsonElement>> StringInsertAsync(AstCliRequest request)
    {
        var languageError = ValidateLanguage(request.Language);
        if (languageError != null) return languageError;

        if (string.IsNullOrWhiteSpace(request.ProjectPath))
            return Fail("projectPath is required");
        if (string.IsNullOrWhiteSpace(request.InsertText))
            return Fail("insertText is required");
        if (request.Position < 0)
            return Fail("position must be >= 0");

#pragma warning disable MCP001
        if (!Directory.Exists(request.ProjectPath))
            return Fail($"Project path not found: {request.ProjectPath}");
#pragma warning restore MCP001

        var result = await StringLiteralEngine.InsertAsync(
            request.ProjectPath, request.FilePath, request.InsertText, request.Position, "insert", request.Filter, request.DryRun);
        return Ok(result, result.Message, AstCliJsonContext.Default.StringInsertResultDto);
    }

    [CliCommand("string_replace", Description = "Replace text in each string literal using literal string or regex pattern", Category = "string", SchemaType = typeof(AstSchemas.StringReplace))]
    private static async Task<OperationResult<JsonElement>> StringReplaceAsync(AstCliRequest request)
    {
        var languageError = ValidateLanguage(request.Language);
        if (languageError != null) return languageError;

        if (string.IsNullOrWhiteSpace(request.ProjectPath))
            return Fail("projectPath is required");
        if (string.IsNullOrWhiteSpace(request.Pattern))
            return Fail("pattern is required");
        if (string.IsNullOrWhiteSpace(request.Replacement))
            return Fail("replacement is required");

#pragma warning disable MCP001
        if (!Directory.Exists(request.ProjectPath))
            return Fail($"Project path not found: {request.ProjectPath}");
#pragma warning restore MCP001

        var result = await StringLiteralEngine.ReplaceAsync(
            request.ProjectPath, request.FilePath, request.Pattern, request.Replacement, request.UseRegex, request.Filter, request.DryRun);
        return Ok(result, result.Message, AstCliJsonContext.Default.StringReplaceResultDto);
    }

    private static OperationResult<JsonElement>? ValidateLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        var provider = LanguageProviderRegistry.GetProvider(language);
        if (provider.Language.IsSupported)
            return null;

        var supported = LanguageProviderRegistry.GetSupportedLanguages();
        var result = new UnsupportedLanguageResultDto
        {
            DetectedLanguage = provider.Language.Name,
            DetectedLanguageDisplayName = provider.Language.DisplayName,
            SupportedLanguages = supported,
            Message = $"Language '{provider.Language.DisplayName}' is not supported. Supported languages: {string.Join(", ", supported)}"
        };

        return new OperationResult<JsonElement>
        {
            Success = false,
            Message = result.Message,
            Data = JsonSerializer.SerializeToElement(result, AstCliJsonContext.Default.UnsupportedLanguageResultDto)
        };
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

    private static OperationResult<JsonElement> Ok<T>(T data, string message = "", JsonTypeInfo<T> typeInfo = null!)
    {
        return new OperationResult<JsonElement>
        {
            Success = true,
            Message = message,
            Data = JsonSerializer.SerializeToElement(data, typeInfo)
        };
    }
}
