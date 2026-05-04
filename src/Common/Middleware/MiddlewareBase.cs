namespace Common.Middleware;

/// <summary>
/// 中间件基类，提供通用的构造函数验证和上下文验证逻辑
/// </summary>
public abstract class MiddlewareBase : IToolMiddleware
{
    /// <summary>
    /// 验证上下文和下一个委托不为null
    /// </summary>
    protected static void ValidateContext(ToolContext context, Func<Task> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
    }

    /// <summary>
    /// 验证服务不为null
    /// </summary>
    protected static T ValidateService<T>(T? service, string paramName) where T : class
    {
        return service ?? throw new ArgumentNullException(paramName);
    }

    /// <summary>
    /// 执行中间件逻辑（子类必须实现）
    /// </summary>
    public abstract Task InvokeAsync(ToolContext context, Func<Task> next);
}

/// <summary>
/// 带日志的中间件基类
/// </summary>
public abstract class LoggingMiddlewareBase : MiddlewareBase
{
    protected readonly ILogger Logger;

    protected LoggingMiddlewareBase(ILogger logger)
    {
        Logger = ValidateService(logger, nameof(logger));
    }

    /// <summary>
    /// 获取中间件名称
    /// </summary>
    protected string MiddlewareName => GetType().Name;
}

/// <summary>
/// 中间件扩展方法
/// </summary>
public static class MiddlewareExtensions
{
    /// <summary>
    /// 检查上下文是否已取消
    /// </summary>
    public static bool IsCancelled(this ToolContext context)
    {
        return context.IsCancelled;
    }

    /// <summary>
    /// 检查上下文是否已有结果
    /// </summary>
    public static bool HasResult(this ToolContext context)
    {
        return !string.IsNullOrEmpty(context.Result);
    }

    /// <summary>
    /// 从上下文中获取用户ID
    /// </summary>
    public static string? GetUserId(this ToolContext context)
    {
        if (context.Items.TryGetValue("UserId", out var userId) && userId is string id)
        {
            return id;
        }

        return null;
    }

    /// <summary>
    /// 从上下文中获取用户角色
    /// </summary>
    public static IReadOnlyList<string> GetUserRoles(this ToolContext context)
    {
        if (context.Items.TryGetValue("UserRoles", out var roles) && roles is IReadOnlyList<string> roleList)
        {
            return roleList;
        }

        if (context.Items.TryGetValue("Roles", out var roles2) && roles2 is IReadOnlyList<string> roleList2)
        {
            return roleList2;
        }

        return [];
    }

    /// <summary>
    /// 从上下文中获取权限列表
    /// </summary>
    public static IReadOnlyList<string> GetPermissions(this ToolContext context)
    {
        if (context.Items.TryGetValue("Permissions", out var permissions) && permissions is IReadOnlyList<string> permList)
        {
            return permList;
        }

        return [];
    }

    /// <summary>
    /// 从上下文中获取输入Schema
    /// </summary>
    public static JsonElement GetInputSchema(this ToolContext context)
    {
        if (context.Items.TryGetValue("InputSchema", out var schema) && schema is JsonElement schemaElement)
        {
            return schemaElement;
        }

        return McpJsonSerializer.EmptyObject;
    }

    /// <summary>
    /// 从上下文中获取所需权限
    /// </summary>
    public static IReadOnlyList<string> GetRequiredPermissions(this ToolContext context)
    {
        if (context.Items.TryGetValue("RequiredPermissions", out var permissions) &&
            permissions is IReadOnlyList<string> permList)
        {
            return permList;
        }

        return [];
    }

    /// <summary>
    /// 从上下文中获取执行上下文
    /// </summary>
    public static Dictionary<string, string> GetExecutionContext(this ToolContext context)
    {
        var executionContext = new Dictionary<string, string>();

        if (context.Items.TryGetValue("Source", out var source) && source is string sourceStr)
        {
            executionContext["Source"] = sourceStr;
        }

        if (context.Items.TryGetValue("RequestId", out var requestId) && requestId is string reqId)
        {
            executionContext["RequestId"] = reqId;
        }

        return executionContext;
    }
}
