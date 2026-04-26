namespace Common.Tools;

/// <summary>
/// 增强型工具描述模型，用于帮助LLM更好地理解工具用途和使用方法
/// </summary>
public sealed class EnhancedToolDescription
{
    /// <summary>
    /// 工具的详细描述，说明工具的核心功能
    /// </summary>
    [JsonPropertyName("detailedDescription")]
    public string DetailedDescription { get; init; } = string.Empty;

    /// <summary>
    /// 使用场景说明，描述何时应该使用此工具
    /// </summary>
    [JsonPropertyName("whenToUse")]
    public string WhenToUse { get; init; } = string.Empty;

    /// <summary>
    /// 不应使用此工具的场景说明
    /// </summary>
    [JsonPropertyName("whenNotToUse")]
    public string WhenNotToUse { get; init; } = string.Empty;

    /// <summary>
    /// 最佳实践建议列表
    /// </summary>
    [JsonPropertyName("bestPractices")]
    public IReadOnlyList<string> BestPractices { get; init; } = [];

    /// <summary>
    /// 常见错误和注意事项
    /// </summary>
    [JsonPropertyName("commonPitfalls")]
    public IReadOnlyList<string> CommonPitfalls { get; init; } = [];

    /// <summary>
    /// 使用示例列表，包含完整的JSON请求示例
    /// </summary>
    [JsonPropertyName("examples")]
    public IReadOnlyList<ToolExample> Examples { get; init; } = [];

    /// <summary>
    /// 相关工具列表，说明与此工具配合使用的其他工具
    /// </summary>
    [JsonPropertyName("relatedTools")]
    public IReadOnlyList<string> RelatedTools { get; init; } = [];

    /// <summary>
    /// 参数详细说明
    /// </summary>
    [JsonPropertyName("parameterDetails")]
    public IReadOnlyList<ParameterDetail> ParameterDetails { get; init; } = [];

    /// <summary>
    /// 返回值说明
    /// </summary>
    [JsonPropertyName("returnDescription")]
    public string ReturnDescription { get; init; } = string.Empty;

    /// <summary>
    /// 生成完整的MCP工具描述文本
    /// </summary>
    public string GenerateFullDescription()
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(DetailedDescription))
            parts.Add(DetailedDescription);

        if (!string.IsNullOrEmpty(WhenToUse))
            parts.Add($"## When to Use\n{WhenToUse}");

        if (!string.IsNullOrEmpty(WhenNotToUse))
            parts.Add($"## When Not to Use\n{WhenNotToUse}");

        if (BestPractices.Count > 0)
            parts.Add($"## Best Practices\n{string.Join("\n", BestPractices.Select((bp, i) => $"{i + 1}. {bp}"))}");

        if (CommonPitfalls.Count > 0)
            parts.Add($"## Common Pitfalls\n{string.Join("\n", CommonPitfalls.Select((cp, i) => $"{i + 1}. {cp}"))}");

        if (Examples.Count > 0)
        {
            var exampleText = string.Join("\n\n", Examples.Select((ex, i) =>
                $"### Example {i + 1}: {ex.Title}\n{ex.Description}\n```json\n{ex.JsonRequest}\n```"));
            parts.Add($"## Examples\n{exampleText}");
        }

        if (RelatedTools.Count > 0)
            parts.Add($"## Related Tools\n{string.Join(", ", RelatedTools)}");

        if (ParameterDetails.Count > 0)
        {
            var paramText = string.Join("\n", ParameterDetails.Select(p =>
                $"- **{p.Name}** ({p.Type}): {p.Description}"));
            parts.Add($"## Parameter Details\n{paramText}");
        }

        if (!string.IsNullOrEmpty(ReturnDescription))
            parts.Add($"## Returns\n{ReturnDescription}");

        return string.Join("\n\n", parts);
    }
}

/// <summary>
/// 工具使用示例
/// </summary>
public sealed class ToolExample
{
    /// <summary>
    /// 示例标题
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 示例描述
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// JSON请求示例
    /// </summary>
    [JsonPropertyName("jsonRequest")]
    public string JsonRequest { get; init; } = string.Empty;

    /// <summary>
    /// 预期响应示例
    /// </summary>
    [JsonPropertyName("expectedResponse")]
    public string? ExpectedResponse { get; init; }
}

/// <summary>
/// 参数详细说明
/// </summary>
public sealed class ParameterDetail
{
    /// <summary>
    /// 参数名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 参数类型
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 参数描述
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 是否必需
    /// </summary>
    [JsonPropertyName("required")]
    public bool Required { get; init; } = true;

    /// <summary>
    /// 默认值
    /// </summary>
    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; init; }

    /// <summary>
    /// 有效值范围或示例
    /// </summary>
    [JsonPropertyName("validValues")]
    public IReadOnlyList<string>? ValidValues { get; init; }
}

/// <summary>
/// 预定义的工具描述模板，用于MemoryCli工具
/// </summary>
public static class MemoryToolDescriptions
{
    /// <summary>
    /// 创建实体工具的增强描述
    /// </summary>
    public static EnhancedToolDescription CreateEntities => new()
    {
        DetailedDescription = "Create multiple new entities in the knowledge graph. Each entity represents a concept, object, person, or any distinguishable thing you want to remember and relate to other entities.",
        WhenToUse = """
- When you need to store structured information about concepts, objects, or entities
- When building a knowledge base from conversations or documents
- When creating nodes that will be connected by relations
- When the user explicitly asks to remember or save information
""",
        WhenNotToUse = """
- When you only need to search existing entities (use search_nodes instead)
- When you want to add observations to existing entities (use add_observations instead)
- When entities already exist and you want to update them (delete and recreate, or add observations)
""",
        BestPractices =
        [
            "Use descriptive and unique entity names that clearly identify the concept",
            "Choose meaningful entity types that help categorize and filter entities later",
            "Add initial observations to provide context about the entity",
            "Create related entities together in a single call for efficiency",
            "Use consistent naming conventions across similar entity types"
        ],
        CommonPitfalls =
        [
            "Creating duplicate entities with slightly different names - check if entity exists first",
            "Using vague entity types like 'thing' or 'item' - be specific",
            "Forgetting to add observations that explain why the entity is important",
            "Not creating relations after creating entities - entities are more useful when connected"
        ],
        Examples = ToolExampleFactory.CreateEntitiesExamples(),
        RelatedTools = ["memory_create_relations", "memory_search_nodes", "memory_add_observations"],
        ParameterDetails =
        [
            new ParameterDetail
            {
                Name = "command",
                Type = "string",
                Description = "Must be 'create_entities'",
                Required = true,
                ValidValues = ["create_entities"]
            },
            new ParameterDetail
            {
                Name = "entities",
                Type = "array",
                Description = "Array of entity objects to create. Each entity requires name and entityType, observations are optional.",
                Required = true
            }
        ],
        ReturnDescription = "Returns a success message with the count of created entities. Duplicate entities (same name) are skipped."
    };

    /// <summary>
    /// 创建关系工具的增强描述
    /// </summary>
    public static EnhancedToolDescription CreateRelations => new()
    {
        DetailedDescription = "Create relations between entities in the knowledge graph. Relations define how entities are connected and provide the structure for navigating the knowledge base.",
        WhenToUse = """
- When you need to establish connections between existing entities
- When building a semantic network or knowledge graph
- When documenting relationships like 'works_for', 'depends_on', 'contains', etc.
- After creating entities that should be linked together
""",
        WhenNotToUse = """
- When the source or target entities don't exist yet (create them first)
- When you only need to search for relations (use search_nodes or read_graph)
- When you want to modify existing relations (delete entities and recreate)
""",
        BestPractices =
        [
            "Ensure both source (from) and target (to) entities exist before creating relations",
            "Use consistent and descriptive relation types (e.g., 'works_for', 'depends_on', 'contains')",
            "Create bidirectional relations when appropriate (e.g., 'manages' and 'managed_by')",
            "Use verbs or verb phrases for relation types to make them self-documenting",
            "Group related relations in a single call for efficiency"
        ],
        CommonPitfalls =
        [
            "Creating relations to non-existent entities - this will fail validation",
            "Using inconsistent relation types (e.g., 'works_for' vs 'worksFor') - pick a convention",
            "Creating duplicate relations - they will be silently skipped",
            "Forgetting to create the reverse relation when needed for bidirectional navigation"
        ],
        Examples = ToolExampleFactory.CreateRelationsExamples(),
        RelatedTools = ["memory_create_entities", "memory_search_nodes", "memory_read_graph"],
        ParameterDetails =
        [
            new ParameterDetail
            {
                Name = "command",
                Type = "string",
                Description = "Must be 'create_relations'",
                Required = true,
                ValidValues = ["create_relations"]
            },
            new ParameterDetail
            {
                Name = "relations",
                Type = "array",
                Description = "Array of relation objects. Each relation requires from (source entity name), to (target entity name), and relationType.",
                Required = true
            }
        ],
        ReturnDescription = "Returns a success message with the count of created relations. Duplicate relations are skipped."
    };

    /// <summary>
    /// 读取图谱工具的增强描述
    /// </summary>
    public static EnhancedToolDescription ReadGraph => new()
    {
        DetailedDescription = "Read the entire knowledge graph, returning all entities and relations. Use this to get a complete view of the stored knowledge.",
        WhenToUse = """
- When you need to see all stored information
- When starting a new conversation to understand existing context
- When exporting or backing up the knowledge graph
- When debugging or verifying the graph structure
""",
        WhenNotToUse = """
- When you only need specific entities (use open_nodes instead)
- When searching for entities matching a pattern (use search_nodes instead)
- When the graph is very large (consider using search or open_nodes for better performance)
""",
        BestPractices =
        [
            "Use this sparingly with large graphs - prefer targeted queries",
            "Consider caching the result if you need to reference it multiple times",
            "Use the result to understand the graph structure before making modifications"
        ],
        CommonPitfalls =
        [
            "Calling this repeatedly when search_nodes would be more efficient",
            "Not handling large result sets appropriately"
        ],
        Examples = ToolExampleFactory.ReadGraphExamples(),
        RelatedTools = ["memory_search_nodes", "memory_open_nodes"],
        ParameterDetails =
        [
            new ParameterDetail
            {
                Name = "command",
                Type = "string",
                Description = "Must be 'read_graph'",
                Required = true,
                ValidValues = ["read_graph"]
            }
        ],
        ReturnDescription = "Returns a GraphData object containing all entities and relations in the knowledge graph."
    };

    /// <summary>
    /// 搜索节点工具的增强描述
    /// </summary>
    public static EnhancedToolDescription SearchNodes => new()
    {
        DetailedDescription = "Search for nodes (entities) in the knowledge graph by keyword. The search matches against entity names, types, and observations.",
        WhenToUse = """
- When looking for entities matching specific criteria
- When you don't know the exact entity name
- When exploring the knowledge graph for relevant information
- When filtering entities by type or content
""",
        WhenNotToUse = """
- When you know the exact entity names (use open_nodes instead)
- When you need all entities (use read_graph instead)
- When searching for relations specifically (search returns related relations automatically)
""",
        BestPractices =
        [
            "Use specific keywords to narrow down results",
            "The search is case-insensitive, so don't worry about capitalization",
            "Search results include related relations, providing context",
            "Combine multiple searches to explore different aspects of the graph"
        ],
        CommonPitfalls =
        [
            "Using too broad search terms that return too many results",
            "Not checking observations for matches - the search includes them",
            "Expecting exact matches - the search uses substring matching"
        ],
        Examples = ToolExampleFactory.SearchNodesExamples(),
        RelatedTools = ["memory_read_graph", "memory_open_nodes"],
        ParameterDetails =
        [
            new ParameterDetail
            {
                Name = "command",
                Type = "string",
                Description = "Must be 'search_nodes'",
                Required = true,
                ValidValues = ["search_nodes"]
            },
            new ParameterDetail
            {
                Name = "query",
                Type = "string",
                Description = "Search keyword. Matches against entity names, types, and observations.",
                Required = true
            }
        ],
        ReturnDescription = "Returns matching entities and their related relations. The search is case-insensitive and uses substring matching."
    };

    /// <summary>
    /// 添加观察工具的增强描述
    /// </summary>
    public static EnhancedToolDescription AddObservations => new()
    {
        DetailedDescription = "Add new observations to an existing entity. Observations are additional facts, notes, or information associated with an entity.",
        WhenToUse = """
- When you want to add new information to an existing entity
- When learning new facts about a person, project, or concept
- When updating entity context without creating a new entity
- When appending notes or comments to tracked items
""",
        WhenNotToUse = """
- When the entity doesn't exist yet (create it first with create_entities)
- When you want to modify existing observations (delete and recreate the entity)
- When you need to track a new related concept (create a new entity with relations)
""",
        BestPractices =
        [
            "Add observations in batches rather than one at a time",
            "Include timestamps or context in observations when relevant",
            "Make observations specific and factual",
            "Use consistent formatting for similar types of observations"
        ],
        CommonPitfalls =
        [
            "Adding duplicate observations - consider what's already stored",
            "Adding observations to non-existent entities - verify entity exists first",
            "Adding vague observations without context - be specific"
        ],
        Examples = ToolExampleFactory.AddObservationsExamples(),
        RelatedTools = ["memory_create_entities", "memory_search_nodes", "memory_open_nodes"],
        ParameterDetails =
        [
            new ParameterDetail
            {
                Name = "command",
                Type = "string",
                Description = "Must be 'add_observations'",
                Required = true,
                ValidValues = ["add_observations"]
            },
            new ParameterDetail
            {
                Name = "name",
                Type = "string",
                Description = "Exact name of the entity to add observations to",
                Required = true
            },
            new ParameterDetail
            {
                Name = "observations",
                Type = "array",
                Description = "Array of observation strings to add to the entity",
                Required = true
            }
        ],
        ReturnDescription = "Returns a success message with the count of added observations."
    };

    /// <summary>
    /// 删除实体工具的增强描述
    /// </summary>
    public static EnhancedToolDescription DeleteEntities => new()
    {
        DetailedDescription = "Delete entities from the knowledge graph. This also removes all relations involving the deleted entities.",
        WhenToUse = """
- When entities are no longer relevant or accurate
- When cleaning up duplicate or erroneous entities
- When resetting or restructuring the knowledge graph
- When explicitly asked to forget or remove information
""",
        WhenNotToUse = """
- When you only want to remove observations (use add_observations with empty list or recreate entity)",
- When you want to archive rather than delete (consider adding an 'archived' observation instead)",
- When other entities depend on the one you're deleting (consider the impact on relations)"
""",
        BestPractices =
        [
            "Verify entity names before deletion using search_nodes or open_nodes",
            "Consider the impact on related entities and relations",
            "Delete in batches when cleaning up multiple entities",
            "Back up important data before deletion (use read_graph)"
        ],
        CommonPitfalls =
        [
            "Deleting entities that are still referenced by relations - relations will be removed too",
            "Not verifying entity names - deletion is case-insensitive but exact match required",
            "Accidentally deleting the wrong entities - always double-check names"
        ],
        Examples = ToolExampleFactory.DeleteEntitiesExamples(),
        RelatedTools = ["memory_read_graph", "memory_search_nodes", "memory_create_entities"],
        ParameterDetails =
        [
            new ParameterDetail
            {
                Name = "command",
                Type = "string",
                Description = "Must be 'delete_entities'",
                Required = true,
                ValidValues = ["delete_entities"]
            },
            new ParameterDetail
            {
                Name = "names",
                Type = "array",
                Description = "Array of entity names to delete. All relations involving these entities will also be removed.",
                Required = true
            }
        ],
        ReturnDescription = "Returns a success message with the count of deleted entities and a note about removed relations."
    };

    /// <summary>
    /// 打开节点工具的增强描述
    /// </summary>
    public static EnhancedToolDescription OpenNodes => new()
    {
        DetailedDescription = "Retrieve specific entities by their exact names. This is more efficient than search when you know the exact entity names.",
        WhenToUse = """
- When you know the exact names of entities you want to retrieve
- When you need specific entities and their relations
- When following up on previously identified entities
- When the user references entities by name
""",
        WhenNotToUse = """
- When you don't know the exact entity names (use search_nodes instead)
- When you need all entities (use read_graph instead)
- When you want to search by content (use search_nodes instead)
""",
        BestPractices =
        [
            "Use this when you have exact entity names from previous operations",
            "Request multiple entities in a single call for efficiency",
            "Combine with search_nodes: search first, then open specific results"
        ],
        CommonPitfalls =
        [
            "Using partial or incorrect names - exact match is required",
            "Not checking if entities exist - non-existent names return empty results",
            "Calling multiple times for different entities when one call would work"
        ],
        Examples = ToolExampleFactory.OpenNodesExamples(),
        RelatedTools = ["memory_search_nodes", "memory_read_graph", "memory_create_entities"],
        ParameterDetails =
        [
            new ParameterDetail
            {
                Name = "command",
                Type = "string",
                Description = "Must be 'open_nodes'",
                Required = true,
                ValidValues = ["open_nodes"]
            },
            new ParameterDetail
            {
                Name = "names",
                Type = "array",
                Description = "Array of exact entity names to retrieve. Names are case-insensitive but must match exactly.",
                Required = true
            }
        ],
        ReturnDescription = "Returns matching entities and their related relations. Non-existent names are silently ignored."
    };

    /// <summary>
    /// 根据命令名称获取工具描述
    /// </summary>
    public static EnhancedToolDescription? GetDescription(string commandName)
    {
        return commandName.ToLowerInvariant() switch
        {
            "create_entities" => CreateEntities,
            "create_relations" => CreateRelations,
            "read_graph" => ReadGraph,
            "search_nodes" => SearchNodes,
            "add_observations" => AddObservations,
            "delete_entities" => DeleteEntities,
            "open_nodes" => OpenNodes,
            _ => null
        };
    }
}
