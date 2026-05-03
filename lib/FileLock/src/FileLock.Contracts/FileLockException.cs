namespace FileLock.Contracts;

public sealed class FileLockException : Exception
{
    public string FilePath { get; }
    public TimeSpan Timeout { get; }

    public FileLockException(string filePath, TimeSpan timeout)
        : base($"Failed to acquire lock for file '{filePath}' within {timeout.TotalSeconds} seconds")
    {
        FilePath = filePath;
        Timeout = timeout;
    }

    public FileLockException(string filePath, TimeSpan timeout, string message)
        : base(message)
    {
        FilePath = filePath;
        Timeout = timeout;
    }

    public FileLockException(string filePath, TimeSpan timeout, string message, Exception innerException)
        : base(message, innerException)
    {
        FilePath = filePath;
        Timeout = timeout;
    }
}
