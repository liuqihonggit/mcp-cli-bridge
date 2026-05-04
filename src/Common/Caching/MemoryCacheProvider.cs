namespace Common.Caching;

/// <summary>
/// 内存缓存提供者实现
/// 支持LRU淘汰策略、时间过期、线程安全
/// 支持AOT编译
/// </summary>
public sealed class MemoryCacheProvider : ICacheProvider
{
    private readonly ConcurrentDictionary<string, CacheEntryHolder> _cache = new();
    private readonly LinkedList<string> _lruList = new();
    private readonly Lock _lruLock = new();
    private readonly ILogger _logger;
    private readonly MemoryCacheOptions _options;

    private long _hits;
    private long _misses;
    private long _evictedCount;
    private DateTimeOffset? _lastCompactTime;
    private bool _disposed;

    /// <summary>
    /// 缓存条目持有者，用于存储不同类型的缓存值
    /// </summary>
    private sealed class CacheEntryHolder
    {
        public object? Value { get; init; }
        public required string Key { get; init; }
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? ExpiresAt { get; init; }
        public DateTimeOffset LastAccessedAt { get; set; } = DateTimeOffset.UtcNow;
        public long AccessCount { get; set; }
        private long _accessCountField;
        public long Size { get; init; }
        public CachePriority Priority { get; init; }
        public LinkedListNode<string>? LruNode { get; set; }

        public bool IsExpired => ExpiresAt.HasValue && DateTimeOffset.UtcNow > ExpiresAt.Value;

        public void MarkAccessed()
        {
            LastAccessedAt = DateTimeOffset.UtcNow;
            AccessCount = Interlocked.Increment(ref _accessCountField);
        }
    }

    /// <summary>
    /// 初始化内存缓存提供者
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">缓存选项</param>
    public MemoryCacheProvider(ILogger logger, MemoryCacheOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? MemoryCacheOptions.Default;
    }

    /// <inheritdoc />
    public int Count => _cache.Count;

    /// <inheritdoc />
    public bool TryGet<T>(string key, out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_cache.TryGetValue(key, out var holder))
        {
            if (holder.IsExpired)
            {
                RemoveEntry(key, holder);
                Interlocked.Increment(ref _misses);
                value = default;
                return false;
            }

            holder.MarkAccessed();
            UpdateLru(holder);

            Interlocked.Increment(ref _hits);
            value = (T?)holder.Value;
            return true;
        }

        Interlocked.Increment(ref _misses);
        value = default;
        return false;
    }

    /// <inheritdoc />
    public ValueTask<CacheResult<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        if (TryGet<T>(key, out var value))
        {
            var holder = _cache.TryGetValue(key, out var h) ? h : null;
            var entry = holder is not null
                ? new CacheEntry<T>
                {
                    Key = holder.Key,
                    Value = (T?)holder.Value!,
                    CreatedAt = holder.CreatedAt,
                    ExpiresAt = holder.ExpiresAt,
                    LastAccessedAt = holder.LastAccessedAt,
                    AccessCount = holder.AccessCount,
                    Size = holder.Size,
                    Priority = holder.Priority
                }
                : null;

            return new(CacheResult.Hit(value!));
        }

        return new(CacheResult.Miss<T>());
    }

    /// <inheritdoc />
    public void SetValue<T>(string key, T value, CacheOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ObjectDisposedException.ThrowIf(_disposed, this);

        options ??= CacheOptions.Default;
        var expiration = options.CalculateExpiration();

        var holder = new CacheEntryHolder
        {
            Value = value,
            Key = key,
            ExpiresAt = expiration,
            Size = options.Size,
            Priority = options.Priority
        };

        AddToLru(holder);

        var existing = _cache.AddOrUpdate(key, holder, (_, oldHolder) =>
        {
            RemoveFromLru(oldHolder);
            return holder;
        });

        if (existing != holder)
        {
            _logger.Log(LogLevel.Debug, $"Cache key '{key}' updated.");
        }

        CheckCapacity();
    }

    /// <inheritdoc />
    public ValueTask SetValueAsync<T>(string key, T value, CacheOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetValue(key, value, options);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public T GetOrCreate<T>(string key, Func<T> factory, CacheOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        if (TryGet<T>(key, out var value))
        {
            return value!;
        }

        var newValue = factory();
        SetValue(key, newValue, options);
        return newValue;
    }

    /// <inheritdoc />
    public async ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, CacheOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        if (TryGet<T>(key, out var value))
        {
            return value!;
        }

        var newValue = await factory(cancellationToken).ConfigureAwait(false);
        SetValue(key, newValue, options);
        return newValue;
    }

    /// <inheritdoc />
    public bool Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_cache.TryRemove(key, out var holder))
        {
            RemoveFromLru(holder);
            _logger.Log(LogLevel.Debug, $"Cache key '{key}' removed.");
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(Remove(key));
    }

    /// <inheritdoc />
    public bool Contains(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_cache.TryGetValue(key, out var holder))
        {
            return !holder.IsExpired;
        }

        return false;
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_lruLock)
        {
            _cache.Clear();
            _lruList.Clear();
        }

        _logger.Log(LogLevel.Info, "Cache cleared.");
    }

    /// <inheritdoc />
    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Clear();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public CacheStatistics GetStatistics()
    {
        var expiredCount = _cache.Values.Count(h => h.IsExpired);
        var totalSize = _cache.Values.Sum(h => h.Size);

        return new CacheStatistics
        {
            Count = _cache.Count,
            Hits = Interlocked.Read(ref _hits),
            Misses = Interlocked.Read(ref _misses),
            TotalSize = totalSize,
            ExpiredCount = expiredCount,
            EvictedCount = Interlocked.Read(ref _evictedCount),
            LastCompactTime = _lastCompactTime
        };
    }

    /// <inheritdoc />
    public void Compact()
    {
        var removedCount = 0;

        // 移除过期项
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            if (Remove(key))
            {
                removedCount++;
            }
        }

        // 如果仍超过容量，执行LRU淘汰
        while (_cache.Count > _options.MaxEntries)
        {
            var keyToEvict = GetLruKeyToEvict();
            if (keyToEvict is null)
            {
                break;
            }

            if (Remove(keyToEvict))
            {
                removedCount++;
                Interlocked.Increment(ref _evictedCount);
            }
        }

        _lastCompactTime = DateTimeOffset.UtcNow;

        if (removedCount > 0)
        {
            _logger.Log(LogLevel.Debug, $"Cache compacted. Removed {removedCount} entries.");
        }
    }

    /// <summary>
    /// 获取LRU淘汰候选键
    /// </summary>
    private string? GetLruKeyToEvict()
    {
        lock (_lruLock)
        {
            // 从LRU列表尾部（最少使用）开始查找可淘汰项
            var node = _lruList.Last;
            while (node is not null)
            {
                if (_cache.TryGetValue(node.Value, out var holder) && holder.Priority != CachePriority.NeverRemove)
                {
                    return node.Value;
                }

                node = node.Previous;
            }
        }

        return null;
    }

    /// <summary>
    /// 检查容量并执行淘汰
    /// </summary>
    private void CheckCapacity()
    {
        if (_cache.Count > _options.MaxEntries)
        {
            Compact();
        }
    }

    /// <summary>
    /// 添加到LRU列表
    /// </summary>
    private void AddToLru(CacheEntryHolder holder)
    {
        lock (_lruLock)
        {
            holder.LruNode = _lruList.AddFirst(holder.Key);
        }
    }

    /// <summary>
    /// 从LRU列表移除
    /// </summary>
    private void RemoveFromLru(CacheEntryHolder holder)
    {
        lock (_lruLock)
        {
            if (holder.LruNode is not null)
            {
                _lruList.Remove(holder.LruNode);
                holder.LruNode = null;
            }
        }
    }

    /// <summary>
    /// 更新LRU位置（移到头部）
    /// </summary>
    private void UpdateLru(CacheEntryHolder holder)
    {
        lock (_lruLock)
        {
            if (holder.LruNode is not null)
            {
                _lruList.Remove(holder.LruNode);
                holder.LruNode = _lruList.AddFirst(holder.Key);
            }
        }
    }

    /// <summary>
    /// 移除缓存条目
    /// </summary>
    private void RemoveEntry(string key, CacheEntryHolder holder)
    {
        _cache.TryRemove(key, out _);
        RemoveFromLru(holder);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Clear();
    }
}

/// <summary>
/// 内存缓存配置选项
/// </summary>
public sealed class MemoryCacheOptions
{
    /// <summary>
    /// 最大缓存条目数量
    /// </summary>
    public int MaxEntries { get; set; } = 1000;

    /// <summary>
    /// 默认过期时间
    /// </summary>
    public TimeSpan? DefaultExpiration { get; set; }

    /// <summary>
    /// 是否启用自动清理
    /// </summary>
    public bool EnableAutoCompact { get; set; } = true;

    /// <summary>
    /// 自动清理间隔
    /// </summary>
    public TimeSpan CompactInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 默认选项
    /// </summary>
    public static MemoryCacheOptions Default => new();

    /// <summary>
    /// 小型缓存选项（100条目）
    /// </summary>
    public static MemoryCacheOptions Small => new() { MaxEntries = 100 };

    /// <summary>
    /// 大型缓存选项（10000条目）
    /// </summary>
    public static MemoryCacheOptions Large => new() { MaxEntries = 10000 };
}
