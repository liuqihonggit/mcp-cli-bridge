namespace Common.Contracts.Middleware;

using IServiceProvider = Common.Contracts.IoC.IServiceProvider;

/// <summary>
/// 工具执行上下文，包含工具调用的完整信息
/// </summary>
public sealed class ToolContext
{
    /// <summary>
    /// 工具名称
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// 工具参数集合
    /// </summary>
    public Dictionary<string, JsonElement> Parameters { get; set; } = [];

    /// <summary>
    /// 工具执行结果（JSON格式）
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// 是否取消执行
    /// </summary>
    public bool IsCancelled { get; set; }

    /// <summary>
    /// 扩展数据项，用于在中间件之间传递自定义数据
    /// </summary>
    public Dictionary<string, object> Items { get; set; } = [];

    /// <summary>
    /// 服务提供器，用于解析依赖服务
    /// </summary>
    public IServiceProvider ServiceProvider { get; set; } = null!;
}
