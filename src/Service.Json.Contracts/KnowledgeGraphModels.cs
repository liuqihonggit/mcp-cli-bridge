namespace Service.Json.Contracts;

/// <summary>
/// 知识图谱实体
/// </summary>
public sealed class KnowledgeGraphEntity
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("entityType")]
    public string EntityType { get; set; } = string.Empty;

    [JsonPropertyName("observations")]
    public List<string> Observations { get; set; } = [];
}

/// <summary>
/// 知识图谱关系
/// </summary>
public sealed class KnowledgeGraphRelation
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("relationType")]
    public string RelationType { get; set; } = string.Empty;
}

/// <summary>
/// 知识图谱数据
/// </summary>
public sealed class KnowledgeGraphData
{
    [JsonPropertyName("entities")]
    public List<KnowledgeGraphEntity> Entities { get; set; } = [];

    [JsonPropertyName("relations")]
    public List<KnowledgeGraphRelation> Relations { get; set; } = [];
}
