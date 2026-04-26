namespace Common.Caching;

/// <summary>
/// 缓存操作结果
/// </summary>
/// <typeparam name="T">缓存值类型</typeparam>
public readonly struct CacheResult<T>
{
    /// <summary>
    /// 是否成功获取缓存
    /// </summary>
    public bool IsHit { get; init; }

    /// <summary>
    /// 缓存值（如果成功获取）
    /// </summary>
    public T? Value { get; init; }

    /// <summary>
    /// 缓存条目（如果存在）
    /// </summary>
    public CacheEntry<T>? Entry { get; init; }

    /// <summary>
    /// 创建缓存命中结果
    /// </summary>
    public static CacheResult<T> Hit(T value, CacheEntry<T>? entry = null) => new()
    {
        IsHit = true,
        Value = value,
        Entry = entry
    };

    /// <summary>
    /// 创建缓存未命中结果
    /// </summary>
    public static CacheResult<T> Miss => new() { IsHit = false };
}
