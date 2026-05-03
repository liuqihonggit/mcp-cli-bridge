using System.Collections.Concurrent;
using FileLock.Contracts;

namespace FileLock;

public sealed class HybridFileMutex : IAsyncDisposable
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> s_intraLocks = new();

    internal static string LockDirectory { get; private set; } =
        Path.Combine(Path.GetTempPath(), "McpHost_FileLocks");
    internal static double LockExpirySeconds { get; private set; } =
        FileAccessOptions.DefaultLockExpirySeconds;

    private readonly string _filePath;
    private readonly string _mutexName;
    private readonly SemaphoreSlim _semaphore;
    private Mutex? _mutex;
    private bool _disposed;

    public string FilePath => _filePath;

    private HybridFileMutex(string filePath, string mutexName, SemaphoreSlim semaphore, Mutex mutex)
    {
        _filePath = filePath;
        _mutexName = mutexName;
        _semaphore = semaphore;
        _mutex = mutex;
    }

    public static HybridFileMutex Acquire(string filePath, TimeSpan timeout)
    {
        var fullPath = Path.GetFullPath(filePath);
        var mutexName = GetMutexName(fullPath);
        var semaphore = s_intraLocks.GetOrAdd(mutexName, _ => new SemaphoreSlim(1, 1));

        var startTime = DateTime.UtcNow;

        if (!semaphore.Wait(timeout))
        {
            throw new TimeoutException(
                $"Failed to acquire intra-process lock for '{filePath}' within {timeout.TotalSeconds}s");
        }

        var remaining = timeout - (DateTime.UtcNow - startTime);
        if (remaining <= TimeSpan.Zero)
        {
            semaphore.Release();
            throw new TimeoutException(
                $"Timeout acquiring lock for '{filePath}' after intra-process wait");
        }

        var mutex = new Mutex(initiallyOwned: false, mutexName);
        bool mutexAcquired;
        try
        {
            mutexAcquired = mutex.WaitOne(remaining);
        }
        catch (AbandonedMutexException)
        {
            mutexAcquired = true;
        }
        catch
        {
            mutex.Dispose();
            semaphore.Release();
            throw;
        }

        if (!mutexAcquired)
        {
            mutex.Dispose();
            semaphore.Release();
            throw new TimeoutException(
                $"Failed to acquire cross-process lock for '{filePath}' within {timeout.TotalSeconds}s");
        }

        WriteLockFile(fullPath);
        return new HybridFileMutex(fullPath, mutexName, semaphore, mutex);
    }

    public static async Task<HybridFileMutex> AcquireAsync(
        string filePath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(filePath);
        var mutexName = GetMutexName(fullPath);
        var semaphore = s_intraLocks.GetOrAdd(mutexName, _ => new SemaphoreSlim(1, 1));

        var startTime = DateTime.UtcNow;

        var acquired = await semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        if (!acquired)
        {
            throw new TimeoutException(
                $"Failed to acquire intra-process lock for '{filePath}' within {timeout.TotalSeconds}s");
        }

        var remaining = timeout - (DateTime.UtcNow - startTime);
        if (remaining <= TimeSpan.Zero)
        {
            semaphore.Release();
            throw new TimeoutException(
                $"Timeout acquiring lock for '{filePath}' after intra-process wait");
        }

        var mutex = new Mutex(initiallyOwned: false, mutexName);
        bool mutexAcquired;
        try
        {
            mutexAcquired = await Task.Run(
                () => mutex.WaitOne(remaining),
                cancellationToken).ConfigureAwait(false);
        }
        catch (AbandonedMutexException)
        {
            mutexAcquired = true;
        }
        catch
        {
            mutex.Dispose();
            semaphore.Release();
            throw;
        }

        if (!mutexAcquired)
        {
            mutex.Dispose();
            semaphore.Release();
            throw new TimeoutException(
                $"Failed to acquire cross-process lock for '{filePath}' within {timeout.TotalSeconds}s");
        }

        WriteLockFile(fullPath);
        return new HybridFileMutex(fullPath, mutexName, semaphore, mutex);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_mutex != null)
        {
            try { DeleteLockFile(_filePath); } catch { }
            try { _mutex.ReleaseMutex(); } catch { }
            _mutex.Dispose();
            _mutex = null;
        }

        try { _semaphore.Release(); } catch { }
    }

    internal void Release()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_mutex != null)
        {
            try { DeleteLockFile(_filePath); } catch { }
            try { _mutex.ReleaseMutex(); } catch { }
            _mutex.Dispose();
            _mutex = null;
        }

        try { _semaphore.Release(); } catch { }
    }

    internal static void Configure(string? lockDirectory, TimeSpan lockExpiry)
    {
        LockDirectory = lockDirectory ?? Path.Combine(Path.GetTempPath(), "McpHost_FileLocks");
        LockExpirySeconds = lockExpiry.TotalSeconds;
        Directory.CreateDirectory(LockDirectory);
    }

    internal static string GetMutexName(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath).ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fullPath)));
        return $"Global\\McpHost_FileLock_{hash}";
    }

    internal static void WriteLockFileStatic(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var lockFilePath = GetLockFilePath(fullPath);
        var directory = Path.GetDirectoryName(lockFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var content = new StringBuilder()
            .AppendLine($"PID:{Environment.ProcessId}")
            .AppendLine($"Time:{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}")
            .AppendLine($"ExpirySeconds:{LockExpirySeconds}")
            .ToString();

        File.WriteAllText(lockFilePath, content);
    }

    internal static void DeleteLockFileStatic(string filePath)
    {
        var lockFilePath = GetLockFilePath(Path.GetFullPath(filePath));
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

    internal static string GetLockFilePath(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fullPath)))[..16];
        var safePath = fullPath
            .Replace(':', '_')
            .Replace(Path.DirectorySeparatorChar, '_')
            .Replace(Path.AltDirectorySeparatorChar, '_');

        return Path.Combine(LockDirectory, $"{safePath}_{hash}.lock");
    }

    private static void WriteLockFile(string filePath)
    {
        var lockFilePath = GetLockFilePath(filePath);
        var directory = Path.GetDirectoryName(lockFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var content = new StringBuilder()
            .AppendLine($"PID:{Environment.ProcessId}")
            .AppendLine($"Time:{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}")
            .AppendLine($"ExpirySeconds:{LockExpirySeconds}")
            .ToString();

        File.WriteAllText(lockFilePath, content);
    }

    private static void DeleteLockFile(string filePath)
    {
        var lockFilePath = GetLockFilePath(filePath);
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
}
