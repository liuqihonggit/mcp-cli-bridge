using Common.Middleware;

namespace McpHost.Middleware;

using IMethodInvoker = Common.Contracts.IMethodInvoker;

/// <summary>
/// MCP工具执行中间件 - 使用 JsonParameterHelper 和 ErrorResponseFactory 重构
/// </summary>
public sealed class McpToolExecutionMiddleware : LoggingMiddlewareBase
{
    public McpToolExecutionMiddleware(ILogger logger) : base(logger)
    {
    }

    public override async Task InvokeAsync(ToolContext context, Func<Task> nextMiddleware)
    {
        ValidateContext(context, nextMiddleware);

        // 检查是否已取消
        if (context.IsCancelled)
        {
            await Logger.DebugAsync($"[{nameof(McpToolExecutionMiddleware)}] 执行已取消: {context.ToolName}");
            return;
        }

        if (!string.IsNullOrEmpty(context.Result))
        {
            await Logger.DebugAsync($"[{nameof(McpToolExecutionMiddleware)}] 已有结果，跳过执行: {context.ToolName}");
            await nextMiddleware();
            return;
        }

        if (!context.Items.TryGetValue("ToolHandler", out var handlerObj) || handlerObj is not ToolHandlerInfo handler)
        {
            await Logger.ErrorAsync($"[{nameof(McpToolExecutionMiddleware)}] 工具处理器未找到: {context.ToolName}");
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

            await Logger.InfoAsync($"[{nameof(McpToolExecutionMiddleware)}] 工具执行完成: {context.ToolName}");
        }
        catch (Exception ex)
        {
            await Logger.ErrorAsync(ex, $"[{nameof(McpToolExecutionMiddleware)}] 工具执行异常: {context.ToolName}");
            ErrorResponseFactory.SetExecutionErrorResult(context, context.ToolName, ex);
        }

        await nextMiddleware();
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
    public IMethodInvoker Invoker { get; init; } = null!;
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
