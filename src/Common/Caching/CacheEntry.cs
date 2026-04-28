namespace Common.Caching;

/// <summary>
/// 缓存条目模型，包含值、过期时间和创建时间
/// 支持AOT编译
/// </summary>
/// <typeparam name="T">缓存值类型</typeparam>
public sealed class CacheEntry<T>
{
    /// <summary>
    /// 缓存值
    /// </summary>
    public required T Value { get; init; }

    /// <summary>
    /// 创建时间（UTC）
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 过期时间（UTC），null表示永不过期
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// 最后访问时间（UTC）
    /// </summary>
    public DateTimeOffset LastAccessedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 访问次数
    /// </summary>
    public long AccessCount { get; set; }

    /// <summary>
    /// 访问计数字段（用于Interlocked操作）
    /// </summary>
    private long _accessCountField;

    /// <summary>
    /// 缓存键
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// 缓存大小（字节），用于LRU淘汰计算
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// 优先级，用于淘汰策略
    /// </summary>
    public CachePriority Priority { get; init; } = CachePriority.Normal;

    /// <summary>
    /// 是否已过期
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && DateTimeOffset.UtcNow > ExpiresAt.Value;

    /// <summary>
    /// 剩余有效时间
    /// </summary>
    public TimeSpan? RemainingTime => ExpiresAt.HasValue
        ? ExpiresAt.Value - DateTimeOffset.UtcNow
        : null;

    /// <summary>
    /// 标记已访问
    /// </summary>
    public void MarkAccessed()
    {
        LastAccessedAt = DateTimeOffset.UtcNow;
        AccessCount = Interlocked.Increment(ref _accessCountField);
    }
}
