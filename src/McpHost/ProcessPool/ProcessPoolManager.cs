namespace McpHost.ProcessPool;

/// <summary>
/// 进程池管理器实现，管理多个CLI工具的进程池
/// </summary>
public sealed class ProcessPoolManager : IProcessPoolManager
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, IProcessPool> _pools;
    private readonly ConcurrentDictionary<string, ProcessPoolOptions> _poolOptions;
    private readonly ConcurrentDictionary<string, string> _executablePaths;
    private readonly object _createLock = new();
    private Timer? _healthCheckTimer;
    private readonly TimeSpan _healthCheckInterval;
    private bool _disposed;

    /// <summary>
    /// 上次健康检查时间
    /// </summary>
    public DateTime LastHealthCheckTime { get; private set; }

    /// <summary>
    /// 上次健康检查移除的进程数
    /// </summary>
    public int LastHealthCheckRemovedCount { get; private set; }

    /// <summary>
    /// 创建进程池管理器
    /// </summary>
    public ProcessPoolManager(ILogger logger, TimeSpan? healthCheckInterval = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pools = new ConcurrentDictionary<string, IProcessPool>(StringComparer.OrdinalIgnoreCase);
        _poolOptions = new ConcurrentDictionary<string, ProcessPoolOptions>(StringComparer.OrdinalIgnoreCase);
        _executablePaths = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _healthCheckInterval = healthCheckInterval ?? TimeSpan.FromMinutes(1);
    }

    /// <summary>
    /// 启动定期健康检查
    /// </summary>
    public void StartHealthCheck()
    {
        ThrowIfDisposed();

        if (_healthCheckTimer != null)
            return;

        _healthCheckTimer = new Timer(
            _ => _ = PerformHealthCheckAsync(),
            null,
            _healthCheckInterval,
            _healthCheckInterval);

        _logger.Log(LogLevel.Info, $"Health check started with interval {_healthCheckInterval.TotalSeconds}s");
    }

    /// <summary>
    /// 手动触发健康检查
    /// </summary>
    public async Task<int> CheckNowAsync()
    {
        return await PerformHealthCheckAsync();
    }

    private async Task<int> PerformHealthCheckAsync()
    {
        if (_disposed)
            return 0;

        try
        {
            LastHealthCheckTime = DateTime.UtcNow;
            var removedCount = await HealthCheckAllAsync();
            LastHealthCheckRemovedCount = removedCount;

            if (removedCount > 0)
            {
                await _logger.LogAsync(LogLevel.Info, $"Health check completed, removed {removedCount} unhealthy processes");
            }

            return removedCount;
        }
        catch (Exception ex)
        {
            await _logger.LogAsync(LogLevel.Error, ex, "Health check failed");
            return 0;
        }
    }

    /// <summary>
    /// 获取或创建指定CLI工具的进程池
    /// </summary>
    public IProcessPool GetOrCreatePool(string cliName, string executablePath, ProcessPoolOptions? options = null)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(cliName))
            throw new ArgumentNullException(nameof(cliName));

        if (string.IsNullOrWhiteSpace(executablePath))
            throw new ArgumentNullException(nameof(executablePath));

        // 存储配置信息
        _executablePaths[cliName] = executablePath;
        _poolOptions[cliName] = options ?? ProcessPoolOptions.Default;

        return _pools.GetOrAdd(cliName, name => CreatePool(name, executablePath, options));
    }

    /// <summary>
    /// 尝试获取指定CLI工具的进程池
    /// </summary>
    public bool TryGetPool(string cliName, out IProcessPool? pool)
    {
        ThrowIfDisposed();
        return _pools.TryGetValue(cliName, out pool);
    }

    /// <summary>
    /// 移除并释放指定CLI工具的进程池
    /// </summary>
    public async Task RemovePoolAsync(string cliName)
    {
        ThrowIfDisposed();

        if (_pools.TryRemove(cliName, out var pool))
        {
            await pool.DisposeAsync();
            _poolOptions.TryRemove(cliName, out _);
            _executablePaths.TryRemove(cliName, out _);

            await _logger.LogAsync(LogLevel.Info, $"Process pool '{cliName}' removed and disposed");
        }
    }

    /// <summary>
    /// 获取所有进程池名称
    /// </summary>
    public IReadOnlyList<string> GetPoolNames()
    {
        return _pools.Keys.ToList().AsReadOnly();
    }

    /// <summary>
    /// 执行所有进程池的健康检查
    /// </summary>
    public async Task<int> HealthCheckAllAsync()
    {
        ThrowIfDisposed();

        var totalRemoved = 0;
        var tasks = _pools.Values.Select(pool => pool.HealthCheckAsync());

        var results = await Task.WhenAll(tasks);
        totalRemoved = results.Sum();

        return totalRemoved;
    }

    /// <summary>
    /// 清理所有进程池的空闲进程
    /// </summary>
    public async Task ClearAllIdleAsync()
    {
        ThrowIfDisposed();

        var tasks = _pools.Values.Select(pool => pool.ClearIdleAsync());
        await Task.WhenAll(tasks);

        await _logger.LogAsync(LogLevel.Info, "Cleared all idle processes from all pools");
    }

    /// <summary>
    /// 获取进程池统计信息
    /// </summary>
    public Dictionary<string, (int Total, int Available)> GetPoolStatistics()
    {
        return _pools.ToDictionary(
            kvp => kvp.Key,
            kvp => (kvp.Value.TotalCount, kvp.Value.AvailableCount));
    }

    private ProcessPool CreatePool(string cliName, string executablePath, ProcessPoolOptions? options)
    {
        lock (_createLock)
        {
            var poolOptions = options ?? ProcessPoolOptions.Default;

            var pool = new ProcessPool(
                cliName,
                executablePath,
                poolOptions,
                _logger);

            _logger.Log(LogLevel.Info, $"Created process pool '{cliName}' with max size {poolOptions.MaxPoolSize}");

            return pool;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // 释放健康检查定时器
        if (_healthCheckTimer != null)
        {
            await _healthCheckTimer.DisposeAsync();
            _healthCheckTimer = null;
            await _logger.LogAsync(LogLevel.Info, "Health check stopped");
        }

        var disposeTasks = _pools.Values.Select(pool => pool.DisposeAsync().AsTask());
        await Task.WhenAll(disposeTasks);

        _pools.Clear();
        _poolOptions.Clear();
        _executablePaths.Clear();

        await _logger.LogAsync(LogLevel.Info, "ProcessPoolManager disposed");
    }

    public void Dispose()
    {
#pragma warning disable VSTHRD002
        DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }
}
