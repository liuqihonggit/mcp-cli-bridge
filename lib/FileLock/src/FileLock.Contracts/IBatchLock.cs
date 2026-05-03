namespace FileLock.Contracts;

public interface IBatchLock : IAsyncDisposable
{
    IReadOnlyList<string> FilePaths { get; }
    bool IsDisposed { get; }
}
