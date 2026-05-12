namespace McpHost.ProcessPool;

/// <summary>
/// 进程池实现，管理CLI进程的生命周期和复用
/// </summary>
public sealed class ProcessPool : IProcessPool
{
    private readonly ILogger _logger;
    private readonly string _executablePath;
    private readonly ProcessPoolOptions _options;
    private readonly ConcurrentBag<PooledProcess> _allProcesses;
    private readonly SemaphoreSlim _poolSemaphore;
    private readonly ConcurrentQueue<PooledProcess> _idleQueue;
    private readonly Timer? _healthCheckTimer;
    private readonly Timer? _cleanupTimer;
    private readonly object _disposeLock = new();

    private bool _disposed;
    private bool _disposing;

    /// <summary>
    /// 进程池名称
    /// </summary>
    public string PoolName { get; }

    /// <summary>
    /// 当前池中可用进程数量
    /// </summary>
    public int AvailableCount => _idleQueue.Count;

    /// <summary>
    /// 当前池中总进程数量
    /// </summary>
    public int TotalCount => _allProcesses.Count;

    /// <summary>
    /// 进程池是否已释放
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// 创建进程池实例
    /// </summary>
    public ProcessPool(
        string poolName,
        string executablePath,
        ProcessPoolOptions options,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(poolName))
            throw new ArgumentNullException(nameof(poolName));

        if (string.IsNullOrWhiteSpace(executablePath))
            throw new ArgumentNullException(nameof(executablePath));

        PoolName = poolName;
        _executablePath = executablePath;
        _options = options ?? ProcessPoolOptions.Default;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _allProcesses = new ConcurrentBag<PooledProcess>();
        _poolSemaphore = new SemaphoreSlim(_options.MaxPoolSize, _options.MaxPoolSize);
        _idleQueue = new ConcurrentQueue<PooledProcess>();

        // 启动健康检查定时器
        if (_options.HealthCheckInterval > TimeSpan.Zero)
        {
            _healthCheckTimer = new Timer(
                _ => _ = HealthCheckAsync(),
                null,
                _options.HealthCheckInterval,
                _options.HealthCheckInterval);
        }

        // 启动空闲清理定时器
        if (_options.IdleTimeout > TimeSpan.Zero)
        {
            var cleanupInterval = TimeSpan.FromMinutes(1);
            _cleanupTimer = new Timer(
                _ => _ = CleanupIdleProcessesAsync(),
                null,
                cleanupInterval,
                cleanupInterval);
        }

        _logger.Log(LogLevel.Info, $"ProcessPool '{PoolName}' created with max size {_options.MaxPoolSize}");
    }

    /// <summary>
    /// 从池中获取一个可用进程
    /// </summary>
    public async Task<PooledProcess> AcquireAsync(CancellationToken cancellationToken = default)
    {
        return await AcquireAsync(_options.AcquireTimeout, cancellationToken);
    }

    /// <summary>
    /// 从池中获取一个可用进程（带超时）
    /// </summary>
    public async Task<PooledProcess> AcquireAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            // 等待获取信号量（控制最大进程数）
            await _poolSemaphore.WaitAsync(cts.Token);

            // 尝试从空闲队列获取
            while (_idleQueue.TryDequeue(out var idleProcess))
            {
                if (idleProcess.IsIdle && IsProcessValid(idleProcess))
                {
                    idleProcess.MarkInUse();
                    await _logger.LogAsync(LogLevel.Debug, $"Reused process PID={idleProcess.ProcessId} from pool '{PoolName}'", CancellationToken.None).ConfigureAwait(false);
                    return idleProcess;
                }

                // 进程无效，移除
                await RemoveProcessAsync(idleProcess);
            }

            // 没有可用进程，创建新进程
            var newProcess = await CreateNewProcessAsync(cts.Token);
            newProcess.MarkInUse();

            await _logger.LogAsync(LogLevel.Info, $"Created new process PID={newProcess.ProcessId} for pool '{PoolName}'", CancellationToken.None).ConfigureAwait(false);
            return newProcess;
        }
        catch (OperationCanceledException)
        {
            _poolSemaphore.Release();
            throw new TimeoutException($"Failed to acquire process from pool '{PoolName}' within {timeout.TotalSeconds}s");
        }
        catch
        {
            _poolSemaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// 将进程归还到池中
    /// </summary>
    public async Task ReleaseAsync(PooledProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);

        ThrowIfDisposed();

        lock (_disposeLock)
        {
            if (_disposed || _disposing)
            {
                process.Dispose();
                return;
            }
        }

        // 检查进程是否仍然有效
        if (!IsProcessValid(process))
        {
            await RemoveProcessAsync(process).ConfigureAwait(false);
            _poolSemaphore.Release();
            return;
        }

        // 检查是否需要回收（使用次数或生命周期）
        if (ShouldRecycle(process, out var recycleReason))
        {
            LogRecycling(process.ProcessId, recycleReason);
            await RemoveProcessAsync(process).ConfigureAwait(false);
            _poolSemaphore.Release();
            return;
        }

        // 标记为空闲并归还队列
        process.MarkIdle();
        _idleQueue.Enqueue(process);
        _poolSemaphore.Release();

        await _logger.LogAsync(LogLevel.Debug, $"Released process PID={process.ProcessId} to pool '{PoolName}'").ConfigureAwait(false);
    }

    /// <summary>
    /// 清理所有空闲进程
    /// </summary>
    public async Task ClearIdleAsync()
    {
        ThrowIfDisposed();

        var removedCount = 0;

        while (_idleQueue.TryDequeue(out var process))
        {
            if (process.IsIdle)
            {
                await RemoveProcessAsync(process);
                _poolSemaphore.Release();
                removedCount++;
            }
        }

        _logger.Log(LogLevel.Info, $"Cleared {removedCount} idle processes from pool '{PoolName}'");
    }

    /// <summary>
    /// 执行健康检查
    /// </summary>
    public async Task<int> HealthCheckAsync()
    {
        ThrowIfDisposed();

        var removedCount = 0;
        var processesToRemove = new List<PooledProcess>();

        foreach (var process in _allProcesses)
        {
            if (!IsProcessValid(process))
            {
                processesToRemove.Add(process);
            }
        }

        foreach (var process in processesToRemove)
        {
            await RemoveProcessAsync(process);
            removedCount++;
        }

        LogHealthCheckResult(removedCount);
        return removedCount;
    }

    #region Private Methods

    #region Health Check Methods

    /// <summary>
    /// 验证进程是否有效（可复用）
    /// </summary>
    private bool IsProcessValid(PooledProcess process)
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
    private bool ShouldRecycle(PooledProcess process, out string? reason)
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
    private bool IsIdleTimeout(PooledProcess process)
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
    private void LogRecycling(int processId, string? reason)
    {
        _logger.Log(LogLevel.Info, $"Process PID={processId} {reason}, recycling in pool '{PoolName}'");
    }

    private void LogHealthCheckResult(int removedCount)
    {
        if (removedCount > 0)
        {
            _logger.Log(LogLevel.Info, $"Health check removed {removedCount} unhealthy processes from pool '{PoolName}'");
        }
    }

    private void LogCleanupResult(int removedCount)
    {
        if (removedCount > 0)
        {
            _logger.Log(LogLevel.Info, $"Cleaned up {removedCount} idle processes from pool '{PoolName}'");
        }
    }

    #endregion

    private async Task<PooledProcess> CreateNewProcessAsync(CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _executablePath,
            Arguments = _options.StartupArguments ?? string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            StandardInputEncoding = Encoding.UTF8
        };

        // 设置工作目录
        if (!string.IsNullOrEmpty(_options.WorkingDirectory))
        {
            startInfo.WorkingDirectory = _options.WorkingDirectory;
        }

        // 设置环境变量
        if (_options.EnvironmentVariables is not null)
        {
            foreach (var (key, value) in _options.EnvironmentVariables)
            {
                startInfo.Environment[key] = value;
            }
        }

        var process = new Process { StartInfo = startInfo };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_options.StartupTimeout);

        try
        {
            process.Start();

            var pooledProcess = new PooledProcess(process, PoolName);
            _allProcesses.Add(pooledProcess);

            return pooledProcess;
        }
        catch (OperationCanceledException)
        {
            process.Dispose();
            throw new TimeoutException($"Process startup timed out for pool '{PoolName}'");
        }
        catch (Exception ex)
        {
            process.Dispose();
            await _logger.LogAsync(LogLevel.Error, ex, $"Failed to create process for pool '{PoolName}'", CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async Task RemoveProcessAsync(PooledProcess process)
    {
        if (!_allProcesses.TryTake(out _))
            return;

        try
        {
            process.Kill();
            await process.WaitForExitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            await _logger.LogAsync(LogLevel.Debug, ex, $"Error while removing process PID={process.ProcessId}").ConfigureAwait(false);
        }
        finally
        {
            process.Dispose();
        }
    }

    private async Task CleanupIdleProcessesAsync()
    {
        if (_disposed || _disposing)
            return;

        var processesToCheck = _idleQueue.ToArray();
        var removedCount = 0;

        foreach (var process in processesToCheck)
        {
            if (!IsProcessValid(process))
            {
                if (_idleQueue.TryDequeue(out var dequeued) && dequeued == process)
                {
                    await RemoveProcessAsync(process);
                    _poolSemaphore.Release();
                    removedCount++;
                }
            }
        }

        LogCleanupResult(removedCount);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    #endregion

    #region IDisposable & IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        lock (_disposeLock)
        {
            if (_disposed || _disposing)
                return;

            _disposing = true;
        }

        _healthCheckTimer?.Dispose();
        _cleanupTimer?.Dispose();

        // 清理所有进程
        foreach (var process in _allProcesses)
        {
            try
            {
                process.Kill();
                await process.WaitForExitAsync(TimeSpan.FromSeconds(5));
                process.Dispose();
            }
            catch
            {
                // 忽略清理错误
            }
        }

        _allProcesses.Clear();
        _poolSemaphore.Dispose();

        lock (_disposeLock)
        {
            _disposed = true;
            _disposing = false;
        }

        _logger.Log(LogLevel.Info, $"ProcessPool '{PoolName}' disposed");
    }

    public void Dispose()
    {
#pragma warning disable VSTHRD002
        DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }

    #endregion
}
