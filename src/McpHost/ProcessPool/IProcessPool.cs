namespace McpHost.ProcessPool;

/// <summary>
/// 进程池接口，定义CLI进程池的核心能力
/// </summary>
public interface IProcessPool : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// 进程池名称（对应CLI工具名称）
    /// </summary>
    string PoolName { get; }

    /// <summary>
    /// 当前池中可用进程数量
    /// </summary>
    int AvailableCount { get; }

    /// <summary>
    /// 当前池中总进程数量（包括使用中的）
    /// </summary>
    int TotalCount { get; }

    /// <summary>
    /// 进程池是否已释放
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    /// 从池中获取一个可用进程
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>池化进程实例</returns>
    Task<PooledProcess> AcquireAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 从池中获取一个可用进程（带超时）
    /// </summary>
    /// <param name="timeout">超时时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>池化进程实例</returns>
    Task<PooledProcess> AcquireAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将进程归还到池中
    /// </summary>
    /// <param name="process">要归还的进程</param>
    Task ReleaseAsync(PooledProcess process);

    /// <summary>
    /// 清理所有空闲进程
    /// </summary>
    Task ClearIdleAsync();

    /// <summary>
    /// 执行健康检查，移除不健康的进程
    /// </summary>
    /// <returns>移除的进程数量</returns>
    Task<int> HealthCheckAsync();
}
