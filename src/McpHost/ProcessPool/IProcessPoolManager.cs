namespace McpHost.ProcessPool;

/// <summary>
/// 进程池管理器接口，管理多个CLI工具的进程池
/// </summary>
public interface IProcessPoolManager : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// 获取或创建指定CLI工具的进程池
    /// </summary>
    /// <param name="cliName">CLI工具名称</param>
    /// <param name="executablePath">可执行文件路径</param>
    /// <param name="options">进程池配置（可选）</param>
    /// <returns>进程池实例</returns>
    IProcessPool GetOrCreatePool(string cliName, string executablePath, ProcessPoolOptions? options = null);

    /// <summary>
    /// 尝试获取指定CLI工具的进程池
    /// </summary>
    /// <param name="cliName">CLI工具名称</param>
    /// <param name="pool">进程池实例</param>
    /// <returns>是否存在</returns>
    bool TryGetPool(string cliName, out IProcessPool? pool);

    /// <summary>
    /// 移除并释放指定CLI工具的进程池
    /// </summary>
    /// <param name="cliName">CLI工具名称</param>
    Task RemovePoolAsync(string cliName);

    /// <summary>
    /// 获取所有进程池名称
    /// </summary>
    IReadOnlyList<string> GetPoolNames();

    /// <summary>
    /// 执行所有进程池的健康检查
    /// </summary>
    /// <returns>移除的总进程数量</returns>
    Task<int> HealthCheckAllAsync();

    /// <summary>
    /// 清理所有进程池的空闲进程
    /// </summary>
    Task ClearAllIdleAsync();
}
