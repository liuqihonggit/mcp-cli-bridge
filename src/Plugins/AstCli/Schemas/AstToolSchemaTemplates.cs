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
}
