using FileLock.Contracts;

namespace FileLock;

public sealed class HybridFileLockProvider : IFileLockProvider, IDisposable
{
    private readonly FileAccessOptions _options;

    public HybridFileLockProvider(FileAccessOptions? options = null)
    {
        _options = options ?? new FileAccessOptions();
        var lockDir = _options.LockDirectory
            ?? Path.Combine(Path.GetTempPath(), "McpHost_FileLocks");
        HybridFileMutex.Configure(lockDir, _options.LockExpiry);
    }

    public async Task<BatchLockResult> AcquireBatchAsync(
        IReadOnlyList<string> filePaths,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (filePaths.Count == 0)
        {
            return BatchLockResult.ErrorResult("No files specified");
        }

        var sorted = filePaths
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var acquired = new List<HybridFileMutex>();
        var startTime = DateTime.UtcNow;

        try
        {
            foreach (var filePath in sorted)
            {
                var remaining = timeout - (DateTime.UtcNow - startTime);
                if (remaining <= TimeSpan.Zero)
                {
                    return BatchLockResult.TimeoutResult(filePath, DateTime.UtcNow - startTime);
                }

                HybridFileMutex mutex;
                try
                {
                    mutex = await HybridFileMutex.AcquireAsync(filePath, remaining, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    return BatchLockResult.TimeoutResult(filePath, DateTime.UtcNow - startTime);
                }
                catch (OperationCanceledException)
                {
                    ReleaseAllReverse(acquired);
                    throw;
                }

                acquired.Add(mutex);
            }

            var batchLock = new HybridBatchLock(acquired);
            return BatchLockResult.SuccessResult(batchLock, DateTime.UtcNow - startTime);
        }
        catch (OperationCanceledException)
        {
            ReleaseAllReverse(acquired);
            throw;
        }
        catch (Exception ex)
        {
            ReleaseAllReverse(acquired);
            return BatchLockResult.ErrorResult($"Failed to acquire batch lock: {ex.Message}");
        }
    }

    public Task<FileLockInfo?> GetLockInfoAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var lockFilePath = HybridFileMutex.GetLockFilePath(filePath);

        if (!File.Exists(lockFilePath))
        {
            return Task.FromResult<FileLockInfo?>(new FileLockInfo
            {
                IsLocked = false,
                LockFilePath = lockFilePath
            });
        }

        try
        {
            var content = File.ReadAllText(lockFilePath);
            var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

            int? processId = null;
            DateTime? lockTime = null;
            double expirySeconds = FileAccessOptions.DefaultLockExpirySeconds;

            foreach (var line in lines)
            {
                if (line.StartsWith("PID:") && int.TryParse(line[4..].Trim(), out var pid))
                    processId = pid;
                else if (line.StartsWith("Time:") && DateTime.TryParse(line[5..].Trim(), out var lt))
                    lockTime = lt.ToUniversalTime();
                else if (line.StartsWith("ExpirySeconds:") && double.TryParse(line[14..].Trim(), out var exp))
                    expirySeconds = exp;
            }

            var isExpired = lockTime.HasValue && (DateTime.UtcNow - lockTime.Value).TotalSeconds > expirySeconds;

            if (isExpired)
            {
                DeleteExpiredLockFile(lockFilePath);
                return Task.FromResult<FileLockInfo?>(new FileLockInfo
                {
                    IsLocked = false,
                    LockFilePath = lockFilePath
                });
            }

            return Task.FromResult<FileLockInfo?>(new FileLockInfo
            {
                IsLocked = true,
                ProcessId = processId,
                LockTime = lockTime,
                ExpiryTime = lockTime?.AddSeconds(expirySeconds),
                LockFilePath = lockFilePath
            });
        }
        catch
        {
            return Task.FromResult<FileLockInfo?>(new FileLockInfo
            {
                IsLocked = false,
                LockFilePath = lockFilePath
            });
        }
    }

    private static void ReleaseAllReverse(List<HybridFileMutex> acquired)
    {
        for (var i = acquired.Count - 1; i >= 0; i--)
        {
            try { acquired[i].Release(); } catch { }
        }
    }

    private static void DeleteExpiredLockFile(string lockFilePath)
    {
        try
        {
            if (File.Exists(lockFilePath))
            {
                File.Delete(lockFilePath);
            }
        }
        catch
        {
        }
    }

    private sealed class HybridBatchLock : IBatchLock
    {
        private readonly List<HybridFileMutex> _mutexes;
        private bool _disposed;

        public IReadOnlyList<string> FilePaths { get; }
        public bool IsDisposed => _disposed;

        public HybridBatchLock(List<HybridFileMutex> mutexes)
        {
            _mutexes = mutexes;
            FilePaths = mutexes.Select(m => m.FilePath).ToList().AsReadOnly();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            for (var i = _mutexes.Count - 1; i >= 0; i--)
            {
                await _mutexes[i].DisposeAsync().ConfigureAwait(false);
            }

            _mutexes.Clear();
        }
    }

    public void Dispose()
    {
    }
}
