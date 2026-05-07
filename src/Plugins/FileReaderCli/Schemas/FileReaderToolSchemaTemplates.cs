namespace FileReaderCli.Schemas;

public static class FileReaderToolSchemaTemplates
{
    public static JsonElement ReadHeadSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("read_head")
                .Build())
            .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Path to the file to read")
                .Build())
            .WithProperty("lineCount", new JsonSchemaPropertyBuilder()
                .WithType("integer")
                .WithDescription("Number of lines to read from the beginning (default: 10)")
                .Build())
            .WithRequired("command", "filePath")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

    public static JsonElement ReadTailSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("read_tail")
                .Build())
            .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Path to the file to read")
                .Build())
            .WithProperty("lineCount", new JsonSchemaPropertyBuilder()
                .WithType("integer")
                .WithDescription("Number of lines to read from the end (default: 10)")
                .Build())
            .WithRequired("command", "filePath")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }
}
