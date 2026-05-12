namespace Common.Logging;

public sealed class Logger : ILogger, IDisposable
{
    private readonly LogOutput _output;
    private readonly LogLevel _minLevel;
    private readonly string _category;
    private readonly string? _filePath;
    private StreamWriter? _fileWriter;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public Logger(
        LogOutput output,
        LogLevel minLevel = LogLevel.Info,
        string? category = null,
        string? filePath = null)
    {
        _output = output;
        _minLevel = minLevel;
        _category = category ?? nameof(Logger);

        if (output.HasFlag(LogOutput.File))
        {
            _filePath = filePath ?? GetDefaultLogPath();
            FileHelper.EnsureDirectory(_filePath);
            _fileWriter = new StreamWriter(_filePath, append: true, encoding: System.Text.Encoding.UTF8);
            _fileWriter.AutoFlush = true;
        }
    }

    public void Log(LogLevel level, string message)
    {
        if (level < _minLevel) return;

        var formatted = FormatMessage(level, message);

        if (_output.HasFlag(LogOutput.StdErr))
        {
            Console.Error.WriteLine(formatted);
        }

        if (_output.HasFlag(LogOutput.File) && _fileWriter is not null)
        {
            _ = WriteToFileAsync(formatted).ConfigureAwait(false);
        }
    }

    public async Task LogAsync(LogLevel level, string message, CancellationToken cancellationToken = default)
    {
        if (level < _minLevel) return;

        var formatted = FormatMessage(level, message);

        if (_output.HasFlag(LogOutput.StdErr))
        {
            await Console.Error.WriteLineAsync(formatted).ConfigureAwait(false);
        }

        if (_output.HasFlag(LogOutput.File) && _fileWriter is not null)
        {
            await WriteToFileAsync(formatted, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WriteToFileAsync(string formatted, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _fileWriter!.WriteLineAsync(formatted).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public void Log(LogLevel level, Exception exception, string message)
    {
        Log(level, $"{message}: {exception.Message}");
        if (exception.StackTrace is not null)
        {
            Log(level, exception.StackTrace);
        }
    }

    public async Task LogAsync(LogLevel level, Exception exception, string message, CancellationToken cancellationToken = default)
    {
        await LogAsync(level, $"{message}: {exception.Message}", cancellationToken).ConfigureAwait(false);
        if (exception.StackTrace is not null)
        {
            await LogAsync(level, exception.StackTrace, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string GetDefaultLogPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyMemoryServer",
            DirectoryNames.Logs,
            $"{DateTime.UtcNow.ToString(DateTimeFormats.LogFile)}{FileExtensions.Jsonl}");
    }

    private string FormatMessage(LogLevel level, string message)
    {
        var timestamp = DateTime.UtcNow.ToString(DateTimeFormats.LogEntry);
        var levelStr = level switch
        {
            LogLevel.Debug => LogLevels.Debug,
            LogLevel.Info => LogLevels.Info,
            LogLevel.Warn => LogLevels.Warn,
            LogLevel.Error => LogLevels.Error,
            _ => LogLevels.Unknown
        };

        return $"[{timestamp}] [{levelStr}] [{_category}] {message}";
    }

    public void Dispose()
    {
        _fileWriter?.Dispose();
        _fileLock.Dispose();
    }
}

public static class LoggerExtensions
{
    public static void Debug(this ILogger logger, string message) => logger.Log(LogLevel.Debug, message);
    public static void Info(this ILogger logger, string message) => logger.Log(LogLevel.Info, message);
    public static void Warn(this ILogger logger, string message) => logger.Log(LogLevel.Warn, message);
    public static void Error(this ILogger logger, string message) => logger.Log(LogLevel.Error, message);
    public static void Error(this ILogger logger, Exception exception, string message) => logger.Log(LogLevel.Error, exception, message);

    public static Task DebugAsync(this ILogger logger, string message, CancellationToken ct = default) => logger.LogAsync(LogLevel.Debug, message, ct);
    public static Task InfoAsync(this ILogger logger, string message, CancellationToken ct = default) => logger.LogAsync(LogLevel.Info, message, ct);
    public static Task WarnAsync(this ILogger logger, string message, CancellationToken ct = default) => logger.LogAsync(LogLevel.Warn, message, ct);
    public static Task ErrorAsync(this ILogger logger, string message, CancellationToken ct = default) => logger.LogAsync(LogLevel.Error, message, ct);
    public static Task ErrorAsync(this ILogger logger, Exception exception, string message, CancellationToken ct = default) => logger.LogAsync(LogLevel.Error, exception, message, ct);
}

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
