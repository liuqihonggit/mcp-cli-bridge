namespace Common.CliProtocol;

/// <summary>
/// Memory CLI 工具 Schema 模板 - 特性驱动版本
/// 使用 ToolSchemaAttribute 特性定义工具Schema
/// </summary>
public static class MemoryToolSchemaTemplates
{
    // 实体属性定义
    private static readonly SchemaPropertyDefinition EntityProperty = new(
        "entity",
        "object",
        [
            new SchemaPropertyDefinition("name", "string"),
            new SchemaPropertyDefinition("entityType", "string"),
            new SchemaPropertyDefinition("observations", "array", new SchemaPropertyDefinition("item", "string"))
        ],
        ["name", "entityType"]
    );

    // 关系属性定义
    private static readonly SchemaPropertyDefinition RelationProperty = new(
        "relation",
        "object",
        [
            new SchemaPropertyDefinition("from", "string"),
            new SchemaPropertyDefinition("to", "string"),
            new SchemaPropertyDefinition("relationType", "string")
        ],
        ["from", "to", "relationType"]
    );

    /// <summary>
    /// 创建实体工具Schema
    /// </summary>
    public static JsonElement CreateEntitiesSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("create_entities")
                .Build())
            .WithProperty("entities", new JsonSchemaPropertyBuilder()
                .WithType("array")
                .WithDescription("List of entities to create")
                .WithItems(CreateEntitySchemaProperty())
                .Build())
            .WithRequired("command", "entities")
            .Build();

        return JsonSchemaSerializer.SerializeToJsonElement(schema);
    }

    /// <summary>
    /// 创建关系工具Schema
    /// </summary>
    public static JsonElement CreateRelationsSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("create_relations")
                .Build())
            .WithProperty("relations", new JsonSchemaPropertyBuilder()
                .WithType("array")
                .WithDescription("List of relations to create")
                .WithItems(CreateRelationSchemaProperty())
                .Build())
            .WithRequired("command", "relations")
            .Build();

        return JsonSchemaSerializer.SerializeToJsonElement(schema);
    }

    /// <summary>
    /// 读取图谱工具Schema
    /// </summary>
    public static JsonElement ReadGraphSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("read_graph")
                .Build())
            .WithRequired("command")
            .Build();

        return JsonSchemaSerializer.SerializeToJsonElement(schema);
    }

    /// <summary>
    /// 搜索节点工具Schema
    /// </summary>
    public static JsonElement SearchNodesSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("search_nodes")
                .Build())
            .WithProperty("query", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Search query")
                .Build())
            .WithRequired("command", "query")
            .Build();

        return JsonSchemaSerializer.SerializeToJsonElement(schema);
    }

    /// <summary>
    /// 添加观察工具Schema
    /// </summary>
    public static JsonElement AddObservationsSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("add_observations")
                .Build())
            .WithProperty("name", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Entity name")
                .Build())
            .WithProperty("observations", new JsonSchemaPropertyBuilder()
                .WithType("array")
                .WithDescription("Observations to add")
                .WithItems(new JsonSchemaPropertyBuilder().WithType("string").Build())
                .Build())
            .WithRequired("command", "name", "observations")
            .Build();

        return JsonSchemaSerializer.SerializeToJsonElement(schema);
    }

    /// <summary>
    /// 删除实体工具Schema
    /// </summary>
    public static JsonElement DeleteEntitiesSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("delete_entities")
                .Build())
            .WithProperty("names", new JsonSchemaPropertyBuilder()
                .WithType("array")
                .WithDescription("Entity names to delete")
                .WithItems(new JsonSchemaPropertyBuilder().WithType("string").Build())
                .Build())
            .WithRequired("command", "names")
            .Build();

        return JsonSchemaSerializer.SerializeToJsonElement(schema);
    }

    /// <summary>
    /// 打开节点工具Schema
    /// </summary>
    public static JsonElement OpenNodesSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("open_nodes")
                .Build())
            .WithProperty("names", new JsonSchemaPropertyBuilder()
                .WithType("array")
                .WithDescription("Entity names to retrieve")
                .WithItems(new JsonSchemaPropertyBuilder().WithType("string").Build())
                .Build())
            .WithRequired("command", "names")
            .Build();

        return JsonSchemaSerializer.SerializeToJsonElement(schema);
    }

    /// <summary>
    /// 获取存储信息工具Schema
    /// </summary>
    public static JsonElement GetStorageInfoSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("get_storage_info")
                .Build())
            .WithRequired("command")
            .Build();

        return JsonSchemaSerializer.SerializeToJsonElement(schema);
    }

    private static JsonSchemaProperty CreateEntitySchemaProperty()
    {
        return new JsonSchemaPropertyBuilder()
            .WithType("object")
            .WithProperties(new Dictionary<string, JsonSchemaProperty>
            {
                ["name"] = new JsonSchemaPropertyBuilder().WithType("string").Build(),
                ["entityType"] = new JsonSchemaPropertyBuilder().WithType("string").Build(),
                ["observations"] = new JsonSchemaPropertyBuilder()
                    .WithType("array")
                    .WithItems(new JsonSchemaPropertyBuilder().WithType("string").Build())
                    .Build()
            })
            .WithRequired("name", "entityType")
            .Build();
    }

    private static JsonSchemaProperty CreateRelationSchemaProperty()
    {
        return new JsonSchemaPropertyBuilder()
            .WithType("object")
            .WithProperties(new Dictionary<string, JsonSchemaProperty>
            {
                ["from"] = new JsonSchemaPropertyBuilder().WithType("string").Build(),
                ["to"] = new JsonSchemaPropertyBuilder().WithType("string").Build(),
                ["relationType"] = new JsonSchemaPropertyBuilder().WithType("string").Build()
            })
            .WithRequired("from", "to", "relationType")
            .Build();
    }
}

/// <summary>
/// File Reader CLI 工具 Schema 模板 - 特性驱动版本
/// </summary>
public static class FileReaderToolSchemaTemplates
{
    /// <summary>
    /// 读取文件头部工具Schema
    /// </summary>
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

        return JsonSchemaSerializer.SerializeToJsonElement(schema);
    }

    /// <summary>
    /// 读取文件尾部工具Schema
    /// </summary>
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

        return JsonSchemaSerializer.SerializeToJsonElement(schema);
    }
}
