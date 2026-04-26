namespace Common.Logging;

/// <summary>
/// 日志作用域，用于自动记录操作开始和结束
/// </summary>
public sealed class LoggingScope : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _operation;
    private readonly string _target;
    private readonly Stopwatch _stopwatch;
    private bool _disposed;

    /// <summary>
    /// 初始化日志作用域
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="operation">操作名称</param>
    /// <param name="target">操作目标</param>
    public LoggingScope(ILogger logger, string operation, string target)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _stopwatch = Stopwatch.StartNew();

        _logger.Log(LogLevel.Info, $"[{_operation}] 开始: {_target}");
    }

    /// <summary>
    /// 记录成功完成
    /// </summary>
    public void Complete()
    {
        if (_disposed) return;

        _stopwatch.Stop();
        _logger.Log(LogLevel.Info, $"[{_operation}] 完成: {_target}, 耗时: {_stopwatch.ElapsedMilliseconds}ms");
        _disposed = true;
    }

    /// <summary>
    /// 记录异常
    /// </summary>
    /// <param name="exception">异常</param>
    public void Fail(Exception exception)
    {
        if (_disposed) return;

        _stopwatch.Stop();
        _logger.Log(LogLevel.Error, exception, $"[{_operation}] 失败: {_target}, 耗时: {_stopwatch.ElapsedMilliseconds}ms");
        _disposed = true;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            Complete();
        }
    }
}

/// <summary>
/// 日志作用域扩展方法
/// </summary>
public static class LoggingScopeExtensions
{
    /// <summary>
    /// 创建日志作用域
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="operation">操作名称</param>
    /// <param name="target">操作目标</param>
    /// <returns>日志作用域</returns>
    public static LoggingScope CreateScope(this ILogger logger, string operation, string target)
    {
        return new LoggingScope(logger, operation, target);
    }

    /// <summary>
    /// 创建工具执行日志作用域
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="toolName">工具名称</param>
    /// <returns>日志作用域</returns>
    public static LoggingScope CreateToolScope(this ILogger logger, string toolName)
    {
        return new LoggingScope(logger, "ToolExecution", toolName);
    }

    /// <summary>
    /// 创建中间件执行日志作用域
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="middlewareName">中间件名称</param>
    /// <param name="toolName">工具名称</param>
    /// <returns>日志作用域</returns>
    public static LoggingScope CreateMiddlewareScope(this ILogger logger, string middlewareName, string toolName)
    {
        return new LoggingScope(logger, $"Middleware:{middlewareName}", toolName);
    }
}
