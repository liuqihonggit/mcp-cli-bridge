namespace Common.Caching;

/// <summary>
/// 文件变更监听器，用于自动使缓存失效
/// 支持AOT编译
/// </summary>
public sealed class FileChangeWatcher : IDisposable
{
    private readonly ICacheProvider _cache;
    private readonly ILogger _logger;
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
    private readonly Lock _watcherLock = new();
    private bool _disposed;

    /// <summary>
    /// 初始化文件变更监听器
    /// </summary>
    /// <param name="cache">缓存提供者</param>
    /// <param name="logger">日志记录器</param>
    public FileChangeWatcher(ICacheProvider cache, ILogger logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 监视指定文件的变更
    /// </summary>
    /// <param name="filePath">文件路径</param>
    public void WatchFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        var fileName = Path.GetFileName(fullPath);

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
        {
            _logger.Log(LogLevel.Warn, $"Invalid file path: {filePath}");
            return;
        }

        lock (_watcherLock)
        {
            var watcherKey = $"{directory}|{fileName}";

            if (_watchers.ContainsKey(watcherKey))
            {
                _logger.Log(LogLevel.Debug, $"Already watching file: {filePath}");
                return;
            }

            try
            {
                var watcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                watcher.Changed += OnFileChanged;
                watcher.Deleted += OnFileDeleted;
                watcher.Renamed += OnFileRenamed;

                _watchers[watcherKey] = watcher;
                _logger.Log(LogLevel.Debug, $"Started watching file: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, $"Failed to watch file: {filePath}");
            }
        }
    }

    /// <summary>
    /// 监视指定目录的变更
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    /// <param name="filter">文件过滤器（可选）</param>
    public void WatchDirectory(string directoryPath, string filter = "*.*")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var fullPath = Path.GetFullPath(directoryPath);

        if (!Directory.Exists(fullPath))
        {
            _logger.Log(LogLevel.Warn, $"Directory does not exist: {directoryPath}");
            return;
        }

        lock (_watcherLock)
        {
            var watcherKey = $"dir:{fullPath}";

            if (_watchers.ContainsKey(watcherKey))
            {
                _logger.Log(LogLevel.Debug, $"Already watching directory: {directoryPath}");
                return;
            }

            try
            {
                var watcher = new FileSystemWatcher(fullPath, filter)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.FileName,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                watcher.Changed += OnDirectoryFileChanged;
                watcher.Deleted += OnDirectoryFileDeleted;
                watcher.Renamed += OnDirectoryFileRenamed;

                _watchers[watcherKey] = watcher;
                _logger.Log(LogLevel.Debug, $"Started watching directory: {directoryPath}");
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, $"Failed to watch directory: {directoryPath}");
            }
        }
    }

    /// <summary>
    /// 停止监视指定文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    public void UnwatchFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        var fileName = Path.GetFileName(fullPath);

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
        {
            return;
        }

        lock (_watcherLock)
        {
            var watcherKey = $"{directory}|{fileName}";

            if (_watchers.TryGetValue(watcherKey, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= OnFileChanged;
                watcher.Deleted -= OnFileDeleted;
                watcher.Renamed -= OnFileRenamed;
                watcher.Dispose();
                _watchers.Remove(watcherKey);
                _logger.Log(LogLevel.Debug, $"Stopped watching file: {filePath}");
            }
        }
    }

    /// <summary>
    /// 停止监视指定目录
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    public void UnwatchDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        var fullPath = Path.GetFullPath(directoryPath);
        var watcherKey = $"dir:{fullPath}";

        lock (_watcherLock)
        {
            if (_watchers.TryGetValue(watcherKey, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= OnDirectoryFileChanged;
                watcher.Deleted -= OnDirectoryFileDeleted;
                watcher.Renamed -= OnDirectoryFileRenamed;
                watcher.Dispose();
                _watchers.Remove(watcherKey);
                _logger.Log(LogLevel.Debug, $"Stopped watching directory: {directoryPath}");
            }
        }
    }

    /// <summary>
    /// 停止所有监视
    /// </summary>
    public void UnwatchAll()
    {
        lock (_watcherLock)
        {
            foreach (var watcher in _watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            _watchers.Clear();
            _logger.Log(LogLevel.Debug, "Stopped watching all files and directories.");
        }
    }

    /// <summary>
    /// 获取当前监视的文件和目录数量
    /// </summary>
    public int WatchCount => _watchers.Count;

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        InvalidateFileCache(e.FullPath, "changed");
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        InvalidateFileCache(e.FullPath, "deleted");
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        InvalidateFileCache(e.OldFullPath, "renamed");
        // 新文件路径的缓存也会在下次访问时失效
    }

    private void OnDirectoryFileChanged(object sender, FileSystemEventArgs e)
    {
        InvalidateFileCache(e.FullPath, "changed");
    }

    private void OnDirectoryFileDeleted(object sender, FileSystemEventArgs e)
    {
        InvalidateFileCache(e.FullPath, "deleted");
    }

    private void OnDirectoryFileRenamed(object sender, RenamedEventArgs e)
    {
        InvalidateFileCache(e.OldFullPath, "renamed");
    }

    private void InvalidateFileCache(string filePath, string reason)
    {
        try
        {
            var cacheKey = CacheKeyGenerator.ForFileContent(filePath);
            if (_cache.Remove(cacheKey))
            {
                _logger.Log(LogLevel.Debug, $"Cache invalidated for file '{filePath}' (reason: {reason})");
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, ex, $"Failed to invalidate cache for file: {filePath}");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnwatchAll();
    }
}
