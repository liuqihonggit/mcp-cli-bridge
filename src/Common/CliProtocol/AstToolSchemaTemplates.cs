namespace Common.CliProtocol;

public static class AstToolSchemaTemplates
{
    public static JsonElement QuerySymbolSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("query_symbol")
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

    public static JsonElement FindReferencesSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("find_references")
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

    public static JsonElement RenameSymbolSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("rename_symbol")
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

    public static JsonElement ReplaceSymbolSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("replace_symbol")
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

    public static JsonElement GetSymbolInfoSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("get_symbol_info")
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
}
