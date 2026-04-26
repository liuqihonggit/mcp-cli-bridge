namespace Service.Json.Contracts;

/// <summary>
/// 创建实体请求示例模型
/// </summary>
public sealed class CreateEntitiesExampleRequest
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "create_entities";

    [JsonPropertyName("entities")]
    public List<ExampleEntity> Entities { get; set; } = [];
}

/// <summary>
/// 示例实体模型
/// </summary>
public sealed class ExampleEntity
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("entityType")]
    public string EntityType { get; set; } = string.Empty;

    [JsonPropertyName("observations")]
    public List<string> Observations { get; set; } = [];
}

/// <summary>
/// 创建关系请求示例模型
/// </summary>
public sealed class CreateRelationsExampleRequest
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "create_relations";

    [JsonPropertyName("relations")]
    public List<ExampleRelation> Relations { get; set; } = [];
}

/// <summary>
/// 示例关系模型
/// </summary>
public sealed class ExampleRelation
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("relationType")]
    public string RelationType { get; set; } = string.Empty;
}

/// <summary>
/// 读取图谱请求示例模型
/// </summary>
public sealed class ReadGraphExampleRequest
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "read_graph";
}

/// <summary>
/// 搜索节点请求示例模型
/// </summary>
public sealed class SearchNodesExampleRequest
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "search_nodes";

    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;
}

/// <summary>
/// 添加观察请求示例模型
/// </summary>
public sealed class AddObservationsExampleRequest
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "add_observations";

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("observations")]
    public List<string> Observations { get; set; } = [];
}

/// <summary>
/// 删除实体请求示例模型
/// </summary>
public sealed class DeleteEntitiesExampleRequest
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "delete_entities";

    [JsonPropertyName("names")]
    public List<string> Names { get; set; } = [];
}

/// <summary>
/// 打开节点请求示例模型
/// </summary>
public sealed class OpenNodesExampleRequest
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "open_nodes";

    [JsonPropertyName("names")]
    public List<string> Names { get; set; } = [];
}
