namespace Common.Contracts.Models;

/// <summary>
/// CLI 请求模型
/// </summary>
public class CliRequest
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("entities")]
    public List<KnowledgeGraphEntity>? Entities { get; set; }

    [JsonPropertyName("relations")]
    public List<KnowledgeGraphRelation>? Relations { get; set; }

    [JsonPropertyName("query")]
    public string? Query { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("observations")]
    public List<string>? Observations { get; set; }

    [JsonPropertyName("names")]
    public List<string>? Names { get; set; }
}
