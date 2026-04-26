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
            FileOperationHelper.EnsureDirectory(_filePath);
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
            _fileLock.Wait();
            try
            {
                _fileWriter.WriteLine(formatted);
            }
            finally
            {
                _fileLock.Release();
            }
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
}
