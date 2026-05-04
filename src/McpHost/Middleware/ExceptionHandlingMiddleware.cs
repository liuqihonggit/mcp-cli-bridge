using Common.Middleware;
using Common.Tools;

namespace McpHost.Middleware;

/// <summary>
/// 异常处理中间件 - 使用 ErrorResponseFactory 和 ExceptionMessageFormatter 重构
/// </summary>
public sealed class ExceptionHandlingMiddleware : LoggingMiddlewareBase
{
    public ExceptionHandlingMiddleware(ILogger logger) : base(logger)
    {
    }

    public override async Task InvokeAsync(ToolContext context, Func<Task> nextMiddleware)
    {
        ValidateContext(context, nextMiddleware);

        try
        {
            await nextMiddleware();
        }
        catch (Exception ex)
        {
            var logLevel = ExceptionMessageFormatter.GetSeverity(ex);
            var logMessage = ExceptionMessageFormatter.FormatForLog(ex, nameof(ExceptionHandlingMiddleware));
            
            Logger.Log(logLevel, ex, $"[{nameof(ExceptionHandlingMiddleware)}] 工具执行异常: {context.ToolName}");

            var errorMessage = ExceptionMessageFormatter.Format(ex);
            ErrorResponseFactory.SetValidationFailedResult(context, [errorMessage]);

            Logger.Info($"[{nameof(ExceptionHandlingMiddleware)}] 异常已处理，阻止后续中间件执行");
        }
    }
}
