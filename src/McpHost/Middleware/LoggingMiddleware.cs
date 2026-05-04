using Common.Middleware;

namespace McpHost.Middleware;

/// <summary>
/// 日志中间件 - 使用 LoggingScope 和 JsonParameterHelper 重构
/// </summary>
public sealed class LoggingMiddleware : LoggingMiddlewareBase
{
    public LoggingMiddleware(ILogger logger) : base(logger)
    {
    }

    public override async Task InvokeAsync(ToolContext context, Func<Task> nextMiddleware)
    {
        ValidateContext(context, nextMiddleware);

        var toolName = context.ToolName;
        var parameters = JsonParameterHelper.SerializeForLog(context.Parameters);

        Logger.Info($"[{nameof(LoggingMiddleware)}] 参数: {parameters}");

        using var scope = Logger.CreateMiddlewareScope(nameof(LoggingMiddleware), toolName);

        try
        {
            await nextMiddleware();

            var result = context.Result ?? "(无结果)";
            Logger.Debug($"[{nameof(LoggingMiddleware)}] 结果: {result}");
        }
        catch (Exception ex)
        {
            scope.Fail(ex);
            throw;
        }
    }
}
