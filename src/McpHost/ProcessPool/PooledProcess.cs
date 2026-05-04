namespace McpHost.ProcessPool;

/// <summary>
/// 进程状态枚举
/// </summary>
public enum ProcessState
{
    /// <summary>
    /// 空闲，可用于获取
    /// </summary>
    Idle,

    /// <summary>
    /// 正在使用中
    /// </summary>
    InUse,

    /// <summary>
    /// 进程已退出或崩溃
    /// </summary>
    Exited,

    /// <summary>
    /// 进程无响应
    /// </summary>
    Unresponsive,

    /// <summary>
    /// 进程已释放
    /// </summary>
    Disposed
}

/// <summary>
/// 池化进程封装类，管理单个CLI进程的生命周期
/// </summary>
public sealed class PooledProcess : IDisposable
{
    private readonly object _lock = new();
    private ProcessState _state = ProcessState.Idle;
    private bool _disposed;
    private DateTime _lastUsedTime;
    private int _usageCount;

    /// <summary>
    /// 进程ID
    /// </summary>
    public int ProcessId => Process?.Id ?? -1;

    /// <summary>
    /// 底层进程实例
    /// </summary>
    public Process? Process { get; private set; }

    /// <summary>
    /// 进程所属的池名称
    /// </summary>
    public string PoolName { get; }

    /// <summary>
    /// 进程创建时间
    /// </summary>
    public DateTime CreatedTime { get; }

    /// <summary>
    /// 最后使用时间
    /// </summary>
    public DateTime LastUsedTime
    {
        get
        {
            lock (_lock)
            {
                return _lastUsedTime;
            }
        }
    }

    /// <summary>
    /// 使用次数
    /// </summary>
    public int UsageCount
    {
        get
        {
            lock (_lock)
            {
                return _usageCount;
            }
        }
    }

    /// <summary>
    /// 进程状态
    /// </summary>
    public ProcessState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// 进程是否健康可用
    /// </summary>
    public bool IsHealthy
    {
        get
        {
            lock (_lock)
            {
                if (_state == ProcessState.Disposed || _state == ProcessState.Exited)
                    return false;

                if (Process is null || Process.HasExited)
                    return false;

                return true;
            }
        }
    }

    /// <summary>
    /// 进程是否空闲
    /// </summary>
    public bool IsIdle
    {
        get
        {
            lock (_lock)
            {
                return _state == ProcessState.Idle && IsHealthy;
            }
        }
    }

    /// <summary>
    /// 进程是否在使用中
    /// </summary>
    public bool IsInUse
    {
        get
        {
            lock (_lock)
            {
                return _state == ProcessState.InUse;
            }
        }
    }

    /// <summary>
    /// 标准输出读取器
    /// </summary>
    public StreamReader? StandardOutput => Process?.StandardOutput;

    /// <summary>
    /// 标准错误读取器
    /// </summary>
    public StreamReader? StandardError => Process?.StandardError;

    /// <summary>
    /// 标准输入写入器
    /// </summary>
    public StreamWriter? StandardInput => Process?.StandardInput;

    /// <summary>
    /// 创建池化进程实例
    /// </summary>
    public PooledProcess(Process process, string poolName)
    {
        Process = process ?? throw new ArgumentNullException(nameof(process));
        PoolName = poolName ?? throw new ArgumentNullException(nameof(poolName));
        CreatedTime = DateTime.UtcNow;
        _lastUsedTime = CreatedTime;
        _usageCount = 0;
        _state = ProcessState.Idle;
    }

    /// <summary>
    /// 标记进程为使用中
    /// </summary>
    public void MarkInUse()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_state == ProcessState.Disposed, this);

            if (_state == ProcessState.InUse)
                throw new InvalidOperationException("Process is already in use");

            _state = ProcessState.InUse;
            _lastUsedTime = DateTime.UtcNow;
            _usageCount++;
        }
    }

    /// <summary>
    /// 标记进程为空闲
    /// </summary>
    public void MarkIdle()
    {
        lock (_lock)
        {
            if (_state == ProcessState.Disposed)
                return;

            _state = ProcessState.Idle;
            _lastUsedTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 标记进程为已退出
    /// </summary>
    public void MarkExited()
    {
        lock (_lock)
        {
            _state = ProcessState.Exited;
        }
    }

    /// <summary>
    /// 标记进程为无响应
    /// </summary>
    public void MarkUnresponsive()
    {
        lock (_lock)
        {
            _state = ProcessState.Unresponsive;
        }
    }

    /// <summary>
    /// 检查进程是否仍在运行
    /// </summary>
    public bool CheckIsRunning()
    {
        lock (_lock)
        {
            if (Process is null || _state == ProcessState.Disposed)
                return false;

            try
            {
                return !Process.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 等待进程退出
    /// </summary>
    public async Task WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (Process is null)
            return;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await Process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // 超时或取消
        }
    }

    /// <summary>
    /// 强制终止进程
    /// </summary>
    public void Kill()
    {
        lock (_lock)
        {
            if (Process is null || _state == ProcessState.Disposed)
                return;

            try
            {
                Process.Kill(entireProcessTree: true);
            }
            catch
            {
                // 忽略终止错误
            }

            _state = ProcessState.Exited;
        }
    }

    /// <summary>
    /// 读取标准输出
    /// </summary>
    public async Task<string> ReadStandardOutputAsync(CancellationToken cancellationToken = default)
    {
        if (StandardOutput is null)
            return string.Empty;

        return await StandardOutput.ReadToEndAsync(cancellationToken);
    }

    /// <summary>
    /// 读取标准错误
    /// </summary>
    public async Task<string> ReadStandardErrorAsync(CancellationToken cancellationToken = default)
    {
        if (StandardError is null)
            return string.Empty;

        return await StandardError.ReadToEndAsync(cancellationToken);
    }

    /// <summary>
    /// 写入标准输入
    /// </summary>
    public async Task WriteStandardInputAsync(string content, CancellationToken cancellationToken = default)
    {
        if (StandardInput is null)
            return;

        await StandardInput.WriteAsync(content.AsMemory(), cancellationToken);
        await StandardInput.FlushAsync(cancellationToken);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _state = ProcessState.Disposed;

            try
            {
                if (Process is { HasExited: false })
                {
                    Process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // 忽略终止错误
            }

            Process?.Dispose();
            Process = null;
        }
    }

    public override string ToString()
    {
        return $"PooledProcess[Pool={PoolName}, PID={ProcessId}, State={State}, UsageCount={UsageCount}]";
    }
}
