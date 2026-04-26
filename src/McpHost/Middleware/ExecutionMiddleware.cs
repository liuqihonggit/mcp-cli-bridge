using Common.Middleware;

namespace McpHost.Middleware;

/// <summary>
/// 执行中间件 - 使用 ErrorResponseFactory 和 LoggingScope 重构
/// </summary>
public sealed class ExecutionMiddleware : LoggingMiddlewareBase
{
    private readonly IToolRegistry _toolRegistry;

    public ExecutionMiddleware(ILogger logger, IToolRegistry toolRegistry) : base(logger)
    {
        _toolRegistry = ValidateService(toolRegistry, nameof(toolRegistry));
    }

    public override async Task InvokeAsync(ToolContext context, Func<Task> next)
    {
        ValidateContext(context, next);

        var toolName = context.ToolName;

        // 检查是否已取消
        if (context.IsCancelled)
        {
            Logger.Debug($"[{nameof(ExecutionMiddleware)}] 执行已取消: {toolName}");
            return;
        }

        // 检查是否已有结果（如缓存命中）
        if (!string.IsNullOrEmpty(context.Result))
        {
            Logger.Debug($"[{nameof(ExecutionMiddleware)}] 已有结果，跳过执行: {toolName}");
            await next();
            return;
        }

        using var scope = Logger.CreateMiddlewareScope(nameof(ExecutionMiddleware), toolName);

        try
        {
            // 获取工具元数据
            if (!_toolRegistry.TryGetTool(toolName, out var metadata) || metadata is null)
            {
                Logger.Error($"[{nameof(ExecutionMiddleware)}] 工具未找到: {toolName}");
                ErrorResponseFactory.SetToolNotFoundResult(context, toolName);
                return;
            }

            // 执行工具
            var result = await _toolRegistry.ExecuteToolAsync(
                toolName,
                context.Parameters);

            // 设置结果
            context.Result = JsonSerializer.Serialize(result, CommonJsonContext.Default.OperationResult);

            Logger.Info($"[{nameof(ExecutionMiddleware)}] 工具执行完成: {toolName}, 成功: {result.Success}");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"[{nameof(ExecutionMiddleware)}] 工具执行异常: {toolName}");
            ErrorResponseFactory.SetExecutionErrorResult(context, toolName, ex);
        }

        await next();
    }
}
