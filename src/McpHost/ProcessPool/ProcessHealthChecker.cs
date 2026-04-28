namespace McpHost.ProcessPool;

/// <summary>
/// 进程健康检查器，定期检查进程池中的进程状态
/// </summary>
public sealed class ProcessHealthChecker : IAsyncDisposable, IDisposable
{
    private readonly ILogger _logger;
    private readonly IProcessPoolManager _poolManager;
    private readonly Timer _healthCheckTimer;
    private readonly TimeSpan _checkInterval;
    private bool _disposed;

    /// <summary>
    /// 上次健康检查时间
    /// </summary>
    public DateTime LastCheckTime { get; private set; }

    /// <summary>
    /// 上次健康检查移除的进程数
    /// </summary>
    public int LastRemovedCount { get; private set; }

    /// <summary>
    /// 创建健康检查器
    /// </summary>
    public ProcessHealthChecker(
        IProcessPoolManager poolManager,
        ILogger logger,
        TimeSpan? checkInterval = null)
    {
        _poolManager = poolManager ?? throw new ArgumentNullException(nameof(poolManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _checkInterval = checkInterval ?? TimeSpan.FromMinutes(1);

        _healthCheckTimer = new Timer(
            _ => _ = PerformHealthCheckAsync(),
            null,
            _checkInterval,
            _checkInterval);

        _logger.Log(LogLevel.Info, $"ProcessHealthChecker started with interval {_checkInterval.TotalSeconds}s");
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
            LastCheckTime = DateTime.UtcNow;
            var removedCount = await _poolManager.HealthCheckAllAsync();
            LastRemovedCount = removedCount;

            if (removedCount > 0)
            {
                _logger.Log(LogLevel.Info, $"Health check completed, removed {removedCount} unhealthy processes");
            }

            return removedCount;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, ex, "Health check failed");
            return 0;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _healthCheckTimer.DisposeAsync();
        _logger.Log(LogLevel.Info, "ProcessHealthChecker stopped");
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
    }
}
