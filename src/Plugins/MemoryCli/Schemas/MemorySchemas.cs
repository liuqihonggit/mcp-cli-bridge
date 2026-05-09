using Common.Contracts.Schema;
using Common.Json.Schema;

namespace MemoryCli.Schemas;

internal static class MemorySchemas
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

    internal sealed class CreateEntities : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("create_entities").Build())
                .WithProperty("entities", new JsonSchemaPropertyBuilder()
                    .WithType("array").WithDescription("List of entities to create")
                    .WithItems(CreateEntitySchemaProperty()).Build())
                .WithRequired("command", "entities")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class CreateRelations : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("create_relations").Build())
                .WithProperty("relations", new JsonSchemaPropertyBuilder()
                    .WithType("array").WithDescription("List of relations to create")
                    .WithItems(CreateRelationSchemaProperty()).Build())
                .WithRequired("command", "relations")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class ReadGraph : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("read_graph").Build())
                .WithRequired("command")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class SearchNodes : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("search_nodes").Build())
                .WithProperty("query", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Search query").Build())
                .WithRequired("command", "query")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class AddObservations : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("add_observations").Build())
                .WithProperty("name", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Entity name").Build())
                .WithProperty("observations", new JsonSchemaPropertyBuilder()
                    .WithType("array").WithDescription("Observations to add")
                    .WithItems(new JsonSchemaPropertyBuilder().WithType("string").Build()).Build())
                .WithRequired("command", "name", "observations")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class DeleteEntities : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("delete_entities").Build())
                .WithProperty("names", new JsonSchemaPropertyBuilder()
                    .WithType("array").WithDescription("Entity names to delete")
                    .WithItems(new JsonSchemaPropertyBuilder().WithType("string").Build()).Build())
                .WithRequired("command", "names")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class DeleteObservations : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("delete_observations").Build())
                .WithProperty("name", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Entity name").Build())
                .WithProperty("observations", new JsonSchemaPropertyBuilder()
                    .WithType("array").WithDescription("Observations to delete")
                    .WithItems(new JsonSchemaPropertyBuilder().WithType("string").Build()).Build())
                .WithRequired("command", "name", "observations")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class DeleteRelations : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("delete_relations").Build())
                .WithProperty("relations", new JsonSchemaPropertyBuilder()
                    .WithType("array").WithDescription("List of relations to delete")
                    .WithItems(CreateRelationSchemaProperty()).Build())
                .WithRequired("command", "relations")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class OpenNodes : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("open_nodes").Build())
                .WithProperty("names", new JsonSchemaPropertyBuilder()
                    .WithType("array").WithDescription("Entity names to retrieve")
                    .WithItems(new JsonSchemaPropertyBuilder().WithType("string").Build()).Build())
                .WithRequired("command", "names")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class GetStorageInfo : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("get_storage_info").Build())
                .WithRequired("command")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class SaveSummary : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("save_summary").Build())
                .WithProperty("title", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Title of the conversation").Build())
                .WithProperty("userMessages", new JsonSchemaPropertyBuilder()
                    .WithType("array").WithDescription("Key user messages from the conversation")
                    .WithItems(new JsonSchemaPropertyBuilder().WithType("string").Build()).Build())
                .WithRequired("command", "title", "userMessages")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class GetRecentSummaries : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("get_recent_summaries").Build())
                .WithProperty("limit", new JsonSchemaPropertyBuilder()
                    .WithType("integer").WithDescription("Maximum number of summaries to return (default: 15)").Build())
                .WithRequired("command")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
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
