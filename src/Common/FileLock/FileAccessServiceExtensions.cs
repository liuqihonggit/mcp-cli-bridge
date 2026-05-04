using Common.Contracts.IoC;

namespace Common.FileLock;

public static class FileAccessServiceExtensions
{
    public static void AddFileAccessService(this IServiceRegistry registry)
    {
        registry.AddSingleton<FileLockService, FileLockService>();
    }
}

public sealed class FileLockService
{
    public static Task<BatchLockResult> AcquireBatchAsync(
        IReadOnlyList<string> filePaths,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        return AsyncFileLock.FileLockService.AcquireBatchAsync(filePaths, timeout, ct);
    }

    public static Task<BatchLockResult> AcquireAsync(
        string filePath,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        return AsyncFileLock.FileLockService.AcquireAsync(filePath, timeout, ct);
    }
}
