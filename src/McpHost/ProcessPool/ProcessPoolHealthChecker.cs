namespace McpHost.ProcessPool;

/// <summary>
/// 进程池健康检查器，负责验证进程状态和空闲超时检查
/// </summary>
public sealed class ProcessPoolHealthChecker
{
    private readonly ProcessPoolOptions _options;
    private readonly ILogger _logger;
    private readonly string _poolName;

    /// <summary>
    /// 创建健康检查器
    /// </summary>
    public ProcessPoolHealthChecker(
        string poolName,
        ProcessPoolOptions options,
        ILogger logger)
    {
        _poolName = poolName ?? throw new ArgumentNullException(nameof(poolName));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 验证进程是否有效（可复用）
    /// </summary>
    public bool IsProcessValid(PooledProcess process)
    {
        if (process.State == ProcessState.Disposed || process.State == ProcessState.Exited)
            return false;

        if (!process.CheckIsRunning())
            return false;

        // 检查空闲超时
        if (_options.IdleTimeout > TimeSpan.Zero && process.IsIdle)
        {
            var idleTime = DateTime.UtcNow - process.LastUsedTime;
            if (idleTime > _options.IdleTimeout)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 检查进程是否需要回收（使用次数或生命周期）
    /// </summary>
    /// <param name="process">要检查的进程</param>
    /// <param name="reason">如果需要回收，返回原因</param>
    /// <returns>是否需要回收</returns>
    public bool ShouldRecycle(PooledProcess process, out string? reason)
    {
        reason = null;

        // 检查是否超过最大使用次数
        if (_options.MaxUsageCount > 0 && process.UsageCount >= _options.MaxUsageCount)
        {
            reason = $"reached max usage count ({_options.MaxUsageCount})";
            return true;
        }

        // 检查是否超过最大生命周期
        if (_options.MaxLifetime > TimeSpan.Zero)
        {
            var lifetime = DateTime.UtcNow - process.CreatedTime;
            if (lifetime >= _options.MaxLifetime)
            {
                reason = $"exceeded max lifetime ({_options.MaxLifetime.TotalMinutes}min)";
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 检查进程是否因空闲超时而需要清理
    /// </summary>
    public bool IsIdleTimeout(PooledProcess process)
    {
        if (_options.IdleTimeout <= TimeSpan.Zero)
            return false;

        if (!process.IsIdle)
            return false;

        var idleTime = DateTime.UtcNow - process.LastUsedTime;
        return idleTime > _options.IdleTimeout;
    }

    /// <summary>
    /// 记录进程回收日志
    /// </summary>
    public void LogRecycling(int processId, string? reason)
    {
        _logger.Log(LogLevel.Info, $"Process PID={processId} {reason}, recycling in pool '{_poolName}'");
    }

    /// <summary>
    /// 记录健康检查结果
    /// </summary>
    public void LogHealthCheckResult(int removedCount)
    {
        if (removedCount > 0)
        {
            _logger.Log(LogLevel.Info, $"Health check removed {removedCount} unhealthy processes from pool '{_poolName}'");
        }
    }

    /// <summary>
    /// 记录清理结果
    /// </summary>
    public void LogCleanupResult(int removedCount)
    {
        if (removedCount > 0)
        {
            _logger.Log(LogLevel.Info, $"Cleaned up {removedCount} idle processes from pool '{_poolName}'");
        }
    }
}
