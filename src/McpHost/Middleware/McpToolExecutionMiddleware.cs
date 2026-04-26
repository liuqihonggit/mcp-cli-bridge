using Common.Middleware;

namespace McpHost.Middleware;

/// <summary>
/// MCP工具执行中间件 - 使用 JsonParameterHelper 和 ErrorResponseFactory 重构
/// </summary>
public sealed class McpToolExecutionMiddleware : LoggingMiddlewareBase
{
    public McpToolExecutionMiddleware(ILogger logger) : base(logger)
    {
    }

    public override async Task InvokeAsync(ToolContext context, Func<Task> next)
    {
        ValidateContext(context, next);

        // 检查是否已取消
        if (context.IsCancelled)
        {
            Logger.Debug($"[{nameof(McpToolExecutionMiddleware)}] 执行已取消: {context.ToolName}");
            return;
        }

        // 检查是否已有结果（如缓存命中）
        if (!string.IsNullOrEmpty(context.Result))
        {
            Logger.Debug($"[{nameof(McpToolExecutionMiddleware)}] 已有结果，跳过执行: {context.ToolName}");
            await next();
            return;
        }

        // 获取工具处理器
        if (!context.Items.TryGetValue("ToolHandler", out var handlerObj) || handlerObj is not ToolHandlerInfo handler)
        {
            Logger.Error($"[{nameof(McpToolExecutionMiddleware)}] 工具处理器未找到: {context.ToolName}");
            ErrorResponseFactory.SetToolNotFoundResult(context, context.ToolName);
            return;
        }

        using var scope = Logger.CreateMiddlewareScope(nameof(McpToolExecutionMiddleware), context.ToolName);

        try
        {
            // 执行工具
            var result = await ExecuteToolAsync(handler, context.Parameters);

            // 设置结果
            context.Result = result;

            Logger.Info($"[{nameof(McpToolExecutionMiddleware)}] 工具执行完成: {context.ToolName}");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"[{nameof(McpToolExecutionMiddleware)}] 工具执行异常: {context.ToolName}");
            ErrorResponseFactory.SetExecutionErrorResult(context, context.ToolName, ex);
        }

        await next();
    }

    private static async Task<string> ExecuteToolAsync(ToolHandlerInfo handler, Dictionary<string, JsonElement> parameters)
    {
        var args = JsonParameterHelper.DeserializeArguments(
            parameters,
            handler.Parameters.Select(p => new Common.Tools.JsonParameterHelper.ParameterInfo
            {
                Name = p.Name,
                Type = p.Type,
                DefaultValue = p.DefaultValue
            }).ToList());

        var result = await handler.Invoker.InvokeAsync(handler.Instance, args).ConfigureAwait(false);

        return JsonParameterHelper.SerializeResult(result);
    }
}

/// <summary>
/// 工具处理器信息
/// </summary>
public sealed class ToolHandlerInfo
{
    public string Name { get; init; } = string.Empty;
    public object Instance { get; init; } = null!;
    public Common.Reflection.IMethodInvoker Invoker { get; init; } = null!;
    public IReadOnlyList<ParameterInfo> Parameters { get; init; } = [];
}

/// <summary>
/// 参数信息
/// </summary>
public sealed class ParameterInfo
{
    public string Name { get; init; } = string.Empty;
    public Type Type { get; init; } = typeof(object);
    public object? DefaultValue { get; init; }
}
