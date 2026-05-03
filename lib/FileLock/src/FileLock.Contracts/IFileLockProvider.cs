namespace FileLock.Contracts;

public interface IFileLockProvider
{
    Task<BatchLockResult> AcquireBatchAsync(
        IReadOnlyList<string> filePaths,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    Task<FileLockInfo?> GetLockInfoAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
