namespace FileLock.Contracts;

public sealed class FileLockInfo
{
    public bool IsLocked { get; init; }
    public int? ProcessId { get; init; }
    public DateTime? LockTime { get; init; }
    public DateTime? ExpiryTime { get; init; }
    public string? LockFilePath { get; init; }
}
