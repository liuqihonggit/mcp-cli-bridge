namespace McpHost.PluginManager;

/// <summary>
/// 插件配置文件根对象
/// </summary>
public sealed class PluginConfiguration
{
    /// <summary>
    /// 配置版本
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// 工具提供者列表
    /// </summary>
    [JsonPropertyName("providers")]
    public List<CliProviderConfiguration> Providers { get; init; } = [];
}

/// <summary>
/// CLI工具提供者配置
/// </summary>
public sealed class CliProviderConfiguration
{
    /// <summary>
    /// 提供者类型，用于工厂创建
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 提供者名称，用于标识和日志记录
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// CLI可执行文件路径（相对或绝对路径）
    /// </summary>
    [JsonPropertyName("executablePath")]
    public string? ExecutablePath { get; init; }

    /// <summary>
    /// CLI命令名称（用于从PackageManager获取路径）
    /// </summary>
    [JsonPropertyName("cliCommand")]
    public string? CliCommand { get; init; }

    /// <summary>
    /// 默认超时时间
    /// </summary>
    [JsonPropertyName("timeout")]
    public string Timeout { get; init; } = "00:00:30";

    /// <summary>
    /// 进程池大小
    /// </summary>
    [JsonPropertyName("processPoolSize")]
    public int ProcessPoolSize { get; init; } = 5;

    /// <summary>
    /// 工具配置列表
    /// </summary>
    [JsonPropertyName("tools")]
    public List<CliToolConfiguration> Tools { get; init; } = [];

    /// <summary>
    /// 解析超时时间为TimeSpan
    /// </summary>
    public TimeSpan GetTimeout() => TimeSpan.TryParse(Timeout, out var result) ? result : TimeSpan.FromSeconds(30);
}

/// <summary>
/// CLI工具配置（用于插件配置文件）
/// 注意：这是配置专用类，比 Common.CliProtocol.CliToolDefinition 多了配置相关字段
/// </summary>
public sealed class CliToolConfiguration
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
    /// 工具类别
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; init; } = "general";

    /// <summary>
    /// 输入参数JSON Schema
    /// </summary>
    [JsonPropertyName("inputSchema")]
    public System.Text.Json.JsonElement InputSchema { get; init; }

    /// <summary>
    /// 默认超时时间（毫秒），覆盖提供者默认值
    /// </summary>
    [JsonPropertyName("defaultTimeout")]
    public int? DefaultTimeout { get; init; }

    /// <summary>
    /// 所需权限列表
    /// </summary>
    [JsonPropertyName("requiredPermissions")]
    public List<string> RequiredPermissions { get; init; } = [];

    /// <summary>
    /// 增强型描述
    /// </summary>
    [JsonPropertyName("enhancedDescription")]
    public EnhancedToolDescription? EnhancedDescription { get; init; }
}
