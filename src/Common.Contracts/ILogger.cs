namespace Common.Contracts;

public interface ILogger
{
    void Log(LogLevel level, string message);
    void Log(LogLevel level, Exception exception, string message);
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
