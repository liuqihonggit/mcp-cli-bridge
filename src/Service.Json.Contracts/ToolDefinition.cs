namespace Service.Json.Contracts;

/// <summary>
/// 统一工具定义模型，用于描述MCP工具的元数据信息
/// </summary>
public sealed class ToolDefinition
{
    /// <summary>
    /// 工具名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 工具描述
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 输入参数的JSON Schema定义
    /// </summary>
    [JsonPropertyName("inputSchema")]
    public JsonElement InputSchema { get; init; }

    /// <summary>
    /// 工具类别，用于分组管理，默认为 "general"
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; init; } = "general";
}
