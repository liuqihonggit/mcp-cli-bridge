namespace FileLock;

using FileLock.Contracts;

public sealed class FileAccessService : IFileAccessService
{
    private readonly IFileLockProvider _lockProvider;
    private readonly FileAccessOptions _options;

    public FileAccessService(IFileLockProvider lockProvider, FileAccessOptions? options = null)
    {
        _lockProvider = lockProvider;
        _options = options ?? new FileAccessOptions();
    }

    public async Task<IBatchLock> AcquireBatchLockAsync(
        IReadOnlyList<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        var result = await _lockProvider.AcquireBatchAsync(filePaths, _options.LockTimeout, cancellationToken).ConfigureAwait(false);

        if (!result.Success || result.BatchLock == null)
        {
            throw new FileLockException(
                result.FailedFile ?? "unknown",
                _options.LockTimeout,
                result.ErrorMessage ?? "Failed to acquire batch lock");
        }

        return result.BatchLock;
    }

    public async Task<T?> ReadJsonAsync<T>(
        string filePath,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) where T : class
    {
        await using var scope = await AcquireSingleLockAsync(filePath, cancellationToken).ConfigureAwait(false);
        return await FileOperationHelper.ReadJsonAsync(filePath, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteJsonAsync<T>(
        string filePath,
        T data,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await AcquireSingleLockAsync(filePath, cancellationToken).ConfigureAwait(false);
        await FileOperationHelper.WriteJsonAsync(filePath, data, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<T>> ReadJsonLinesAsync<T>(
        string filePath,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) where T : class
    {
        await using var scope = await AcquireSingleLockAsync(filePath, cancellationToken).ConfigureAwait(false);
        return await FileOperationHelper.ReadJsonLinesAsync(filePath, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    public async Task AppendJsonLineAsync<T>(
        string filePath,
        T data,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await AcquireSingleLockAsync(filePath, cancellationToken).ConfigureAwait(false);
        await FileOperationHelper.AppendJsonLineAsync(filePath, data, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveJsonLinesAsync<T>(
        string filePath,
        IReadOnlyList<T> items,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await AcquireSingleLockAsync(filePath, cancellationToken).ConfigureAwait(false);
        await FileOperationHelper.SaveJsonLinesAsync(filePath, items, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> ReadTextAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await AcquireSingleLockAsync(filePath, cancellationToken).ConfigureAwait(false);
        return await FileOperationHelper.ReadTextAsync(filePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteTextAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await AcquireSingleLockAsync(filePath, cancellationToken).ConfigureAwait(false);
        await FileOperationHelper.WriteTextAsync(filePath, content, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> SafeMoveAsync(
        string sourcePath,
        string destPath,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await AcquireBatchLockAsync([sourcePath, destPath], cancellationToken).ConfigureAwait(false);
        return await FileOperationHelper.SafeMoveAsync(sourcePath, destPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> CopyAsync(
        string sourcePath,
        string destPath,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await AcquireBatchLockAsync([sourcePath, destPath], cancellationToken).ConfigureAwait(false);
        return await FileOperationHelper.CopyAsync(sourcePath, destPath, cancellationToken).ConfigureAwait(false);
    }

    public bool SafeDelete(string filePath)
    {
        var result = _lockProvider.AcquireBatchAsync([filePath], _options.LockTimeout).GetAwaiter().GetResult();

        if (!result.Success || result.BatchLock == null)
        {
            return false;
        }

        var scope = result.BatchLock;

        try
        {
            return FileOperationHelper.SafeDelete(filePath);
        }
        finally
        {
            scope.DisposeAsync().GetAwaiter().GetResult();
        }
    }

    public long GetFileSize(string filePath) => FileOperationHelper.GetFileSize(filePath);

    public bool FileExists(string filePath) => File.Exists(filePath);

    public void EnsureDirectory(string filePath) => FileOperationHelper.EnsureDirectory(filePath);

    private async Task<IBatchLock> AcquireSingleLockAsync(string filePath, CancellationToken cancellationToken)
    {
        return await AcquireBatchLockAsync([filePath], cancellationToken).ConfigureAwait(false);
    }
}
