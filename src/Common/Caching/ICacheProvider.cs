namespace Common.Caching;

/// <summary>
/// 缓存提供者接口，定义缓存操作的抽象
/// 支持AOT编译，线程安全
/// </summary>
public interface ICacheProvider : IDisposable
{
    /// <summary>
    /// 获取缓存值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">缓存值（如果存在且未过期）</param>
    /// <returns>是否成功获取</returns>
    bool TryGet<T>(string key, out T? value);

    /// <summary>
    /// 异步获取缓存值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>缓存值（如果存在且未过期）</returns>
    ValueTask<CacheResult<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置缓存值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">缓存值</param>
    /// <param name="options">缓存选项（可选）</param>
    void Set<T>(string key, T value, CacheOptions? options = null);

    /// <summary>
    /// 异步设置缓存值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">缓存值</param>
    /// <param name="options">缓存选项（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask SetAsync<T>(string key, T value, CacheOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取或创建缓存值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="factory">值工厂（当缓存不存在时调用）</param>
    /// <param name="options">缓存选项（可选）</param>
    /// <returns>缓存值或新创建的值</returns>
    T GetOrCreate<T>(string key, Func<T> factory, CacheOptions? options = null);

    /// <summary>
    /// 异步获取或创建缓存值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="factory">异步值工厂（当缓存不存在时调用）</param>
    /// <param name="options">缓存选项（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>缓存值或新创建的值</returns>
    ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, CacheOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移除缓存项
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <returns>是否成功移除</returns>
    bool Remove(string key);

    /// <summary>
    /// 异步移除缓存项
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功移除</returns>
    ValueTask<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查缓存是否存在且未过期
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <returns>是否存在</returns>
    bool Contains(string key);

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    void Clear();

    /// <summary>
    /// 异步清空所有缓存
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    /// <returns>缓存统计</returns>
    CacheStatistics GetStatistics();

    /// <summary>
    /// 触发缓存清理（过期项清理）
    /// </summary>
    void Compact();

    /// <summary>
    /// 缓存项数量
    /// </summary>
    int Count { get; }
}
