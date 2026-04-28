namespace Common.Contracts.Caching;

/// <summary>
/// 缓存选项配置
/// </summary>
public sealed class CacheOptions
{
    /// <summary>
    /// 绝对过期时间（相对于当前时间）
    /// </summary>
    public TimeSpan? AbsoluteExpiration { get; init; }

    /// <summary>
    /// 滑动过期时间（每次访问后重置）
    /// </summary>
    public TimeSpan? SlidingExpiration { get; init; }

    /// <summary>
    /// 缓存优先级
    /// </summary>
    public CachePriority Priority { get; init; } = CachePriority.Normal;

    /// <summary>
    /// 缓存大小（字节），用于大小限制
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// 过期时间（便捷属性，设置绝对过期时间）
    /// </summary>
    public TimeSpan? Expiration
    {
        get => AbsoluteExpiration;
        init => AbsoluteExpiration = value;
    }

    /// <summary>
    /// 创建默认缓存选项（5分钟绝对过期）
    /// </summary>
    public static CacheOptions Default => new() { AbsoluteExpiration = TimeSpan.FromMinutes(5) };

    /// <summary>
    /// 创建短期缓存选项（1分钟）
    /// </summary>
    public static CacheOptions ShortLived => new() { AbsoluteExpiration = TimeSpan.FromMinutes(1) };

    /// <summary>
    /// 创建中期缓存选项（5分钟）
    /// </summary>
    public static CacheOptions MediumLived => new() { AbsoluteExpiration = TimeSpan.FromMinutes(5) };

    /// <summary>
    /// 创建长期缓存选项（30分钟）
    /// </summary>
    public static CacheOptions LongLived => new() { AbsoluteExpiration = TimeSpan.FromMinutes(30), Priority = CachePriority.High };

    /// <summary>
    /// 创建永不过期的缓存选项
    /// </summary>
    public static CacheOptions NeverExpire => new() { Priority = CachePriority.NeverRemove };

    /// <summary>
    /// 计算实际过期时间
    /// </summary>
    /// <returns>过期时间，null表示永不过期</returns>
    public DateTimeOffset? CalculateExpiration()
    {
        if (AbsoluteExpiration.HasValue)
        {
            return DateTimeOffset.UtcNow.Add(AbsoluteExpiration.Value);
        }

        if (SlidingExpiration.HasValue)
        {
            return DateTimeOffset.UtcNow.Add(SlidingExpiration.Value);
        }

        return null;
    }
}
