namespace Common.PluginManager;

/// <summary>
/// 工具注册选项，封装工具注册的参数配置
/// </summary>
public sealed class ToolRegistrationOptions
{
    /// <summary>
    /// 工具实例
    /// </summary>
    public required object ToolInstance { get; init; }

    /// <summary>
    /// 工具名称（可选，默认从特性获取）
    /// </summary>
    public string? ToolName { get; init; }

    /// <summary>
    /// 工具描述（可选，默认从特性获取）
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 是否为异步工具
    /// </summary>
    public bool IsAsync { get; init; } = true;

    /// <summary>
    /// 是否允许覆盖已存在的工具注册
    /// </summary>
    public bool AllowOverride { get; init; } = false;
}
