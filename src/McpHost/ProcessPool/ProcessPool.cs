namespace McpHost.ProcessPool;

/// <summary>
/// 进程池实现，管理CLI进程的生命周期和复用
/// </summary>
public sealed class ProcessPool : IProcessPool
{
    private readonly ILogger _logger;
    private readonly string _executablePath;
    private readonly ProcessPoolOptions _options;
    private readonly ProcessPoolHealthChecker _healthChecker;
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
        _healthChecker = new ProcessPoolHealthChecker(poolName, _options, logger);

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
                if (idleProcess.IsIdle && _healthChecker.IsProcessValid(idleProcess))
                {
                    idleProcess.MarkInUse();
                    _logger.Log(LogLevel.Debug, $"Reused process PID={idleProcess.ProcessId} from pool '{PoolName}'");
                    return idleProcess;
                }

                // 进程无效，移除
                await RemoveProcessAsync(idleProcess);
            }

            // 没有可用进程，创建新进程
            var newProcess = await CreateNewProcessAsync(cts.Token);
            newProcess.MarkInUse();

            _logger.Log(LogLevel.Info, $"Created new process PID={newProcess.ProcessId} for pool '{PoolName}'");
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
        if (process is null)
            throw new ArgumentNullException(nameof(process));

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
        if (!_healthChecker.IsProcessValid(process))
        {
            await RemoveProcessAsync(process).ConfigureAwait(false);
            _poolSemaphore.Release();
            return;
        }

        // 检查是否需要回收（使用次数或生命周期）
        if (_healthChecker.ShouldRecycle(process, out var recycleReason))
        {
            _healthChecker.LogRecycling(process.ProcessId, recycleReason);
            await RemoveProcessAsync(process).ConfigureAwait(false);
            _poolSemaphore.Release();
            return;
        }

        // 标记为空闲并归还队列
        process.MarkIdle();
        _idleQueue.Enqueue(process);
        _poolSemaphore.Release();

        _logger.Log(LogLevel.Debug, $"Released process PID={process.ProcessId} to pool '{PoolName}'");
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
            if (!_healthChecker.IsProcessValid(process))
            {
                processesToRemove.Add(process);
            }
        }

        foreach (var process in processesToRemove)
        {
            await RemoveProcessAsync(process);
            removedCount++;
        }

        _healthChecker.LogHealthCheckResult(removedCount);
        return removedCount;
    }

    #region Private Methods

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
            _logger.Log(LogLevel.Error, ex, $"Failed to create process for pool '{PoolName}'");
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
            _logger.Log(LogLevel.Debug, ex, $"Error while removing process PID={process.ProcessId}");
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
            if (!_healthChecker.IsProcessValid(process))
            {
                if (_idleQueue.TryDequeue(out var dequeued) && dequeued == process)
                {
                    await RemoveProcessAsync(process);
                    _poolSemaphore.Release();
                    removedCount++;
                }
            }
        }

        _healthChecker.LogCleanupResult(removedCount);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ProcessPool));
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
        // 同步 Dispose 使用 Wait 等待异步操作完成
        // 这是 Dispose 模式的标准做法
        DisposeAsync().AsTask().Wait();
    }

    #endregion
}
