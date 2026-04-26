namespace Common.Caching;

/// <summary>
/// 缓存键生成器
/// 支持AOT编译
/// </summary>
public static class CacheKeyGenerator
{
    /// <summary>
    /// 生成工具元数据缓存键
    /// </summary>
    /// <param name="providerName">提供者名称</param>
    /// <returns>缓存键</returns>
    public static string ForToolMetadata(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        return $"tool:metadata:{providerName}";
    }

    /// <summary>
    /// 生成工具列表缓存键
    /// </summary>
    /// <returns>缓存键</returns>
    public static string ForToolList()
    {
        return "tool:list:all";
    }

    /// <summary>
    /// 生成工具执行结果缓存键
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="parameters">参数</param>
    /// <returns>缓存键</returns>
    public static string ForToolExecution(string toolName, IReadOnlyDictionary<string, JsonElement> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(parameters);

        var paramHash = ComputeParameterHash(parameters);
        return $"tool:exec:{toolName}:{paramHash}";
    }

    /// <summary>
    /// 生成文件内容缓存键
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>缓存键</returns>
    public static string ForFileContent(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var normalizedPath = filePath.Replace('\\', '/').ToLowerInvariant();
        return $"file:content:{normalizedPath}";
    }

    /// <summary>
    /// 生成IO操作缓存键
    /// </summary>
    /// <param name="operation">操作类型</param>
    /// <param name="identifier">标识符</param>
    /// <returns>缓存键</returns>
    public static string ForIOperation(string operation, string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return $"io:{operation}:{identifier}";
    }

    /// <summary>
    /// 计算参数哈希值
    /// </summary>
    private static string ComputeParameterHash(IReadOnlyDictionary<string, JsonElement> parameters)
    {
        if (parameters.Count == 0)
        {
            return "empty";
        }

        // 按键排序确保一致性
        var sortedKeys = parameters.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        var sb = new StringBuilder();

        foreach (var key in sortedKeys)
        {
            sb.Append(key);
            sb.Append(':');
            sb.Append(parameters[key].ToString());
            sb.Append(';');
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// 缓存常量
/// </summary>
public static class CacheKeys
{
    /// <summary>
    /// 工具元数据缓存前缀
    /// </summary>
    public const string ToolMetadataPrefix = "tool:metadata:";

    /// <summary>
    /// 工具列表缓存键
    /// </summary>
    public const string ToolList = "tool:list:all";

    /// <summary>
    /// 工具执行缓存前缀
    /// </summary>
    public const string ToolExecutionPrefix = "tool:exec:";

    /// <summary>
    /// 文件内容缓存前缀
    /// </summary>
    public const string FileContentPrefix = "file:content:";

    /// <summary>
    /// IO操作缓存前缀
    /// </summary>
    public const string IOPrefix = "io:";
}
