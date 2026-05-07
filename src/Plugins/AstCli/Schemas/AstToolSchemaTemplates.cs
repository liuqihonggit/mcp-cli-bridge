namespace AstCli.Schemas;

public static class AstToolSchemaTemplates
{
    public static JsonElement SymbolQuerySchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("symbol_query")
                .Build())
            .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Root path of the C# project to analyze")
                .Build())
            .WithProperty("symbolName", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Symbol name to search for")
                .Build())
            .WithProperty("scope", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Search scope: 'project' (all files) or 'file' (top-level only)")
                .Build())
            .WithRequired("command", "projectPath", "symbolName")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

    public static JsonElement ReferenceFindSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("reference_find")
                .Build())
            .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Root path of the C# project")
                .Build())
            .WithProperty("symbolName", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Symbol name to find references for")
                .Build())
            .WithRequired("command", "projectPath", "symbolName")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

    public static JsonElement SymbolRenameSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("symbol_rename")
                .Build())
            .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Root path of the C# project")
                .Build())
            .WithProperty("symbolName", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Current symbol name")
                .Build())
            .WithProperty("newName", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("New symbol name")
                .Build())
            .WithRequired("command", "projectPath", "symbolName", "newName")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

    public static JsonElement SymbolReplaceSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("symbol_replace")
                .Build())
            .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Root path of the C# project")
                .Build())
            .WithProperty("symbolName", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Old symbol name to replace")
                .Build())
            .WithProperty("newName", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("New symbol name")
                .Build())
            .WithRequired("command", "projectPath", "symbolName", "newName")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

    public static JsonElement SymbolInfoSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("symbol_info")
                .Build())
            .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Path to the C# source file")
                .Build())
            .WithProperty("lineNumber", new JsonSchemaPropertyBuilder()
                .WithType("integer")
                .WithDescription("Line number (0-based)")
                .Build())
            .WithProperty("columnNumber", new JsonSchemaPropertyBuilder()
                .WithType("integer")
                .WithDescription("Column number (0-based)")
                .Build())
            .WithRequired("command", "filePath", "lineNumber", "columnNumber")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

    public static JsonElement WorkspaceOverviewSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("workspace_overview")
                .Build())
            .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Root path of the C# project")
                .Build())
            .WithRequired("command", "projectPath")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

    public static JsonElement FileContextSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("file_context")
                .Build())
            .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Root path of the C# project")
                .Build())
            .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Path to the C# source file to analyze")
                .Build())
            .WithRequired("command", "projectPath", "filePath")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

    public static JsonElement DiagnosticsSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("diagnostics")
                .Build())
            .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Root path of the C# project")
                .Build())
            .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Optional: specific file to check (scans entire project if omitted)")
                .Build())
            .WithRequired("command", "projectPath")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

    public static JsonElement SymbolOutlineSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("symbol_outline")
                .Build())
            .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Path to the C# source file")
                .Build())
            .WithRequired("command", "filePath")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

    public static JsonElement StringQuerySchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("string_query")
                .Build())
            .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Root path of the C# project")
                .Build())
            .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Optional: specific file to scan (scans entire project if omitted)")
                .Build())
            .WithProperty("prefix", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Filter strings by prefix (e.g. 'MCP' returns only strings starting with 'MCP')")
                .Build())
            .WithProperty("filter", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Filter strings by content (substring match)")
                .Build())
            .WithRequired("command", "projectPath")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

    public static JsonElement StringPrefixSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("string_prefix")
                .Build())
            .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Root path of the C# project")
                .Build())
            .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Optional: specific file to modify (modifies entire project if omitted)")
                .Build())
            .WithProperty("insertText", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Text to insert as prefix at position 0 of each string")
                .Build())
            .WithProperty("filter", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Only modify strings containing this substring")
                .Build())
            .WithProperty("dryRun", new JsonSchemaPropertyBuilder()
                .WithType("boolean")
                .WithDescription("Preview mode: do not actually modify files (default: false)")
                .Build())
            .WithRequired("command", "projectPath", "insertText")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

    public static JsonElement StringSuffixSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("string_suffix")
                .Build())
            .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Root path of the C# project")
                .Build())
            .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Optional: specific file to modify (modifies entire project if omitted)")
                .Build())
            .WithProperty("insertText", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Text to insert as suffix at the end of each string")
                .Build())
            .WithProperty("filter", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Only modify strings containing this substring")
                .Build())
            .WithProperty("dryRun", new JsonSchemaPropertyBuilder()
                .WithType("boolean")
                .WithDescription("Preview mode: do not actually modify files (default: false)")
                .Build())
            .WithRequired("command", "projectPath", "insertText")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

    public static JsonElement StringInsertSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("string_insert")
                .Build())
            .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Root path of the C# project")
                .Build())
            .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Optional: specific file to modify (modifies entire project if omitted)")
                .Build())
            .WithProperty("insertText", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Text to insert at the specified position")
                .Build())
            .WithProperty("position", new JsonSchemaPropertyBuilder()
                .WithType("integer")
                .WithDescription("Character position to insert at (0=beginning, equals string length=end)")
                .Build())
            .WithProperty("filter", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Only modify strings containing this substring")
                .Build())
            .WithProperty("dryRun", new JsonSchemaPropertyBuilder()
                .WithType("boolean")
                .WithDescription("Preview mode: do not actually modify files (default: false)")
                .Build())
            .WithRequired("command", "projectPath", "insertText", "position")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }
}
