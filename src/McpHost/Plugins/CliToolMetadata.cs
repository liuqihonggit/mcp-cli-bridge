namespace McpHost.Plugins;

/// <summary>
/// CLI工具元数据实现
/// </summary>
public sealed class CliToolMetadata : IToolMetadata
{
    /// <summary>
    /// 工具名称，唯一标识符
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 工具描述，简要说明工具用途
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 工具类别，用于分组管理
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; init; } = "general";

    /// <summary>
    /// 输入参数的JSON Schema定义
    /// </summary>
    [JsonPropertyName("inputSchema")]
    public JsonElement InputSchema { get; init; }

    /// <summary>
    /// 默认超时时间（毫秒）
    /// </summary>
    [JsonPropertyName("defaultTimeout")]
    public int DefaultTimeout { get; init; } = 30000;

    /// <summary>
    /// 执行此工具所需的权限列表
    /// </summary>
    [JsonPropertyName("requiredPermissions")]
    public IReadOnlyList<string> RequiredPermissions { get; init; } = [];

    /// <summary>
    /// CLI命令名称，用于路由到正确的CLI可执行文件
    /// </summary>
    [JsonPropertyName("cliCommand")]
    public string CliCommand { get; init; } = string.Empty;

    /// <summary>
    /// 增强型描述信息，用于LLM理解
    /// </summary>
    [JsonPropertyName("enhancedDescription")]
    public EnhancedToolDescription? EnhancedDescription { get; init; }

    /// <summary>
    /// 从 ToolManifestDefinition 创建 CliToolMetadata
    /// </summary>
    public static CliToolMetadata FromManifestDefinition(
        string name,
        string description,
        string cliCommand,
        JsonElement inputSchema,
        string category = "general",
        int defaultTimeout = 30000,
        EnhancedToolDescription? enhancedDescription = null)
    {
        return new CliToolMetadata
        {
            Name = name,
            Description = description,
            Category = category,
            InputSchema = inputSchema,
            DefaultTimeout = defaultTimeout,
            CliCommand = cliCommand,
            EnhancedDescription = enhancedDescription
        };
    }

    /// <summary>
    /// 获取完整的工具描述，包含增强描述信息
    /// </summary>
    public string GetFullDescription()
    {
        if (EnhancedDescription is null)
            return Description;

        return $"{Description}\n\n{EnhancedDescription.GenerateFullDescription()}";
    }
}
