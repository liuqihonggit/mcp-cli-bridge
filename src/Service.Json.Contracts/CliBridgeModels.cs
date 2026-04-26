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
/// 工具信息结果
/// </summary>
public sealed class ToolInfoResult
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public JsonElement InputSchema { get; set; }
}

/// <summary>
/// 工具列表结果
/// </summary>
public sealed class ToolListResult
{
    [JsonPropertyName("totalTools")]
    public int TotalTools { get; set; }

    [JsonPropertyName("providers")]
    public IReadOnlyList<string> Providers { get; set; } = [];

    [JsonPropertyName("tools")]
    public List<ToolSummary> Tools { get; set; } = [];
}

/// <summary>
/// 工具摘要
/// </summary>
public sealed class ToolSummary
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;
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
