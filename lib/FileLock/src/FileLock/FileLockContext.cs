namespace FileLock;

public static class FileLockContext
{
    private static readonly AsyncLocal<HashSet<string>?> _lockedFiles = new();
    private static readonly AsyncLocal<bool> _enforcementEnabled = new() { Value = true };
    private static readonly TimeSpan DefaultLockTimeout = TimeSpan.FromSeconds(5);

    public static bool EnforcementEnabled
    {
        get => _enforcementEnabled.Value;
        set => _enforcementEnabled.Value = value;
    }

    public static bool IsFileLocked(string filePath)
    {
        var lockedFiles = _lockedFiles.Value;
        if (lockedFiles == null || lockedFiles.Count == 0)
            return false;

        var fullPath = Path.GetFullPath(filePath);
        return lockedFiles.Contains(fullPath);
    }

    public static bool HasAnyLock()
    {
        var lockedFiles = _lockedFiles.Value;
        return lockedFiles != null && lockedFiles.Count > 0;
    }

    public static IDisposable EnterLock(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var hybridMutex = HybridFileMutex.Acquire(fullPath, DefaultLockTimeout);

        _lockedFiles.Value ??= [];
        _lockedFiles.Value.Add(fullPath);

        return new LockScope(fullPath, hybridMutex);
    }

    private sealed class LockScope : IDisposable
    {
        private readonly string _filePath;
        private readonly HybridFileMutex _hybridMutex;
        private bool _disposed;

        public LockScope(string filePath, HybridFileMutex hybridMutex)
        {
            _filePath = filePath;
            _hybridMutex = hybridMutex;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _lockedFiles.Value?.Remove(_filePath);
            _hybridMutex.Release();
        }
    }

    public static IDisposable DisableEnforcement()
    {
        var previousValue = _enforcementEnabled.Value;
        _enforcementEnabled.Value = false;

        return new EnforcementScope(previousValue);
    }

    private sealed class EnforcementScope : IDisposable
    {
        private readonly bool _previousValue;
        private bool _disposed;

        public EnforcementScope(bool previousValue)
        {
            _previousValue = previousValue;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _enforcementEnabled.Value = _previousValue;
        }
    }
}
