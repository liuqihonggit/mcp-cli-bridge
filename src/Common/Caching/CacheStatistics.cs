namespace Common.Caching;

/// <summary>
/// 缓存统计信息
/// </summary>
public sealed class CacheStatistics
{
    /// <summary>
    /// 当前缓存项数量
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// 总命中次数
    /// </summary>
    public long Hits { get; init; }

    /// <summary>
    /// 总未命中次数
    /// </summary>
    public long Misses { get; init; }

    /// <summary>
    /// 命中率
    /// </summary>
    public double HitRate => Hits + Misses > 0 ? (double)Hits / (Hits + Misses) : 0;

    /// <summary>
    /// 总请求数
    /// </summary>
    public long TotalRequests => Hits + Misses;

    /// <summary>
    /// 已过期被清理的项数
    /// </summary>
    public long EvictedCount { get; init; }

    /// <summary>
    /// 过期项数量
    /// </summary>
    public int ExpiredCount { get; init; }

    /// <summary>
    /// 当前缓存大小（字节）
    /// </summary>
    public long TotalSize { get; init; }

    /// <summary>
    /// 最后清理时间
    /// </summary>
    public DateTimeOffset? LastCompactTime { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 创建空统计
    /// </summary>
    public static CacheStatistics Empty => new();
}
