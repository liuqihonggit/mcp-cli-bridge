namespace Service.Json.Contracts;

/// <summary>
/// 工具搜索结果
/// </summary>
public sealed class ToolSearchResult
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// 插件描述符 - MCP层面只展示插件级别信息，不暴露CLI内部工具名
/// </summary>
public sealed class PluginDescriptor
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("commandCount")]
    public int CommandCount { get; set; }

    [JsonPropertyName("hasDocumentation")]
    public bool HasDocumentation { get; set; }
}

/// <summary>
/// 命令描述符 - 插件内部的CLI命令（仅通过tool_describe按需获取）
/// </summary>
public sealed class CommandDescriptor
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public JsonElement InputSchema { get; set; }
}

/// <summary>
/// 插件详情结果（tool_describe的返回值）
/// </summary>
public sealed class PluginDescribeResult
{
    [JsonPropertyName("pluginName")]
    public string PluginName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("commands")]
    public List<CommandDescriptor> Commands { get; set; } = [];
}

/// <summary>
/// 工具列表结果 - 只显示插件级别摘要
/// </summary>
public sealed class ToolListResult
{
    [JsonPropertyName("totalPlugins")]
    public int TotalPlugins { get; set; }

    [JsonPropertyName("plugins")]
    public List<PluginDescriptor> Plugins { get; set; } = [];
}

/// <summary>
/// 提供者列表结果
/// </summary>
public sealed class ProviderListResult
{
    [JsonPropertyName("totalProviders")]
    public int TotalProviders { get; set; }

    [JsonPropertyName("providers")]
    public List<ProviderInfo> Providers { get; set; } = [];
}

/// <summary>
/// 提供者信息
/// </summary>
public sealed class ProviderInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("toolCount")]
    public int ToolCount { get; set; }
}

/// <summary>
/// 错误响应
/// </summary>
public sealed class ErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;
}

/// <summary>
/// 包状态结果
/// </summary>
public sealed class PackageStatusResult
{
    [JsonPropertyName("isInstalled")]
    public bool IsInstalled { get; set; }

    [JsonPropertyName("packageName")]
    public string PackageName { get; set; } = string.Empty;

    [JsonPropertyName("toolsDirectory")]
    public string ToolsDirectory { get; set; } = string.Empty;
}

/// <summary>
/// 包安装结果
/// </summary>
public sealed class PackageInstallResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("packageName")]
    public string PackageName { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// CLI 操作计数结果
/// </summary>
public sealed class CountResult
{
    [JsonPropertyName("count")]
    public int Count { get; set; }
}

/// <summary>
/// CLI 删除结果
/// </summary>
public sealed class DeleteResult
{
    [JsonPropertyName("deleted")]
    public int Deleted { get; set; }
}
