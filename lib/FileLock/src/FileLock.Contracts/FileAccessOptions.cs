namespace FileLock.Contracts;

public sealed class FileAccessOptions
{
    public const int DefaultLockTimeoutSeconds = 5;
    public const int DefaultLockExpirySeconds = 30;
    public const int DefaultRetryCount = 3;
    public const int DefaultRetryDelayMs = 100;

    public TimeSpan LockTimeout { get; init; } = TimeSpan.FromSeconds(DefaultLockTimeoutSeconds);
    public TimeSpan LockExpiry { get; init; } = TimeSpan.FromSeconds(DefaultLockExpirySeconds);
    public int MaxRetries { get; init; } = DefaultRetryCount;
    public int RetryDelayMs { get; init; } = DefaultRetryDelayMs;
    public string? LockDirectory { get; init; }
    public bool EnableFallbackOnLockFailure { get; init; } = true;
}
