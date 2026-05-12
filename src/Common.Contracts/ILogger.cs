namespace Common.Contracts;

public interface ILogger
{
    void Log(LogLevel level, string message);
    void Log(LogLevel level, Exception exception, string message);
    Task LogAsync(LogLevel level, string message, CancellationToken cancellationToken = default);
    Task LogAsync(LogLevel level, Exception exception, string message, CancellationToken cancellationToken = default);
}

[Flags]
public enum LogOutput
{
    None = 0,
    StdErr = 1,
    File = 2,
    StdErrAndFile = StdErr | File
}

public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error
}
