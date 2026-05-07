namespace MemoryCli.Schemas;

public static class MemoryToolSchemaTemplates
{
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

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

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

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

    public static JsonElement ReadGraphSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("read_graph")
                .Build())
            .WithRequired("command")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

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

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

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

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

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

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

    public static JsonElement DeleteObservationsSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("delete_observations")
                .Build())
            .WithProperty("name", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithDescription("Entity name")
                .Build())
            .WithProperty("observations", new JsonSchemaPropertyBuilder()
                .WithType("array")
                .WithDescription("Observations to delete")
                .WithItems(new JsonSchemaPropertyBuilder().WithType("string").Build())
                .Build())
            .WithRequired("command", "name", "observations")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

    public static JsonElement DeleteRelationsSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("delete_relations")
                .Build())
            .WithProperty("relations", new JsonSchemaPropertyBuilder()
                .WithType("array")
                .WithDescription("List of relations to delete")
                .WithItems(CreateRelationSchemaProperty())
                .Build())
            .WithRequired("command", "relations")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

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

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
    }

    public static JsonElement GetStorageInfoSchema()
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst("get_storage_info")
                .Build())
            .WithRequired("command")
            .Build();

        return JsonSchemaBuilder.SerializeToJsonElement(schema);
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
