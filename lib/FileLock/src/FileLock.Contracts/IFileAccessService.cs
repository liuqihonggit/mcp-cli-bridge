namespace FileLock.Contracts;

public interface IFileAccessService
{
    Task<IBatchLock> AcquireBatchLockAsync(
        IReadOnlyList<string> filePaths,
        CancellationToken cancellationToken = default);

    Task<T?> ReadJsonAsync<T>(
        string filePath,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) where T : class;

    Task WriteJsonAsync<T>(
        string filePath,
        T data,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default);

    Task<List<T>> ReadJsonLinesAsync<T>(
        string filePath,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) where T : class;

    Task AppendJsonLineAsync<T>(
        string filePath,
        T data,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default);

    Task SaveJsonLinesAsync<T>(
        string filePath,
        IReadOnlyList<T> items,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default);

    Task<string?> ReadTextAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task WriteTextAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken = default);

    Task<bool> SafeMoveAsync(
        string sourcePath,
        string destPath,
        CancellationToken cancellationToken = default);

    Task<bool> CopyAsync(
        string sourcePath,
        string destPath,
        CancellationToken cancellationToken = default);

    bool SafeDelete(string filePath);

    long GetFileSize(string filePath);

    bool FileExists(string filePath);

    void EnsureDirectory(string filePath);
}
