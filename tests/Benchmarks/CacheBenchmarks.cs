namespace Benchmarks;

/// <summary>
/// 缓存性能基准测试
/// 测试缓存命中、过期、淘汰等操作的性能
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
[RankColumn]
public class CachePerformanceBenchmark
{
    private MemoryCacheProvider _cache = null!;
    private string[] _keys = null!;
    private readonly Mock<ILogger> _loggerMock = new();

    [Params(100, 1000, 10000)]
    public int EntryCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _cache = new MemoryCacheProvider(_loggerMock.Object, new MemoryCacheOptions
        {
            MaxEntries = EntryCount * 2
        });

        _keys = new string[EntryCount];
        for (int i = 0; i < EntryCount; i++)
        {
            _keys[i] = $"key{i}";
            _cache.Set(_keys[i], $"value{i}");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cache.Dispose();
    }

    [Benchmark(Description = "Cache Hit - Get existing key")]
    public bool CacheHit()
    {
        var key = _keys[Random.Shared.Next(EntryCount)];
        return _cache.TryGet<string>(key, out _);
    }

    [Benchmark(Description = "Cache Miss - Get non-existing key")]
    public bool CacheMiss()
    {
        return _cache.TryGet<string>("nonexistent_key", out _);
    }

    [Benchmark(Description = "Cache Set - Update existing key")]
    public void CacheSet()
    {
        var key = _keys[Random.Shared.Next(EntryCount)];
        _cache.Set(key, "updated_value");
    }

    [Benchmark(Description = "Cache Set - Add new key")]
    public void CacheSetNew()
    {
        var key = $"new_key_{Guid.NewGuid()}";
        _cache.Set(key, "new_value");
    }

    [Benchmark(Description = "Cache GetOrCreate - Existing key")]
    public string CacheGetOrCreate_Existing()
    {
        var key = _keys[Random.Shared.Next(EntryCount)];
        return _cache.GetOrCreate(key, () => "factory_value");
    }

    [Benchmark(Description = "Cache GetOrCreate - New key")]
    public string CacheGetOrCreate_New()
    {
        var key = $"new_key_{Guid.NewGuid()}";
        return _cache.GetOrCreate(key, () => "factory_value");
    }

    [Benchmark(Description = "Cache Remove")]
    public bool CacheRemove()
    {
        var key = $"remove_key_{Random.Shared.Next(1000)}";
        _cache.Set(key, "temp");
        return _cache.Remove(key);
    }
}

/// <summary>
/// 缓存并发性能测试
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
public class CacheConcurrencyBenchmark
{
    private MemoryCacheProvider _cache = null!;
    private readonly Mock<ILogger> _loggerMock = new();

    [Params(10, 100)]
    public int ConcurrentOperations { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _cache = new MemoryCacheProvider(_loggerMock.Object, new MemoryCacheOptions
        {
            MaxEntries = 10000
        });
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cache.Dispose();
    }

    [Benchmark(Description = "Concurrent Read/Write")]
    public void ConcurrentReadWrite()
    {
        var tasks = new Task[ConcurrentOperations];
        for (int i = 0; i < ConcurrentOperations; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
            {
                var key = $"key{index % 100}";
                if (index % 2 == 0)
                {
                    _cache.Set(key, $"value{index}");
                }
                else
                {
                    _cache.TryGet<string>(key, out _);
                }
            });
        }
        Task.WaitAll(tasks);
    }
}

/// <summary>
/// 缓存淘汰性能测试
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
public class CacheEvictionBenchmark
{
    private MemoryCacheProvider _cache = null!;
    private readonly Mock<ILogger> _loggerMock = new();

    [Params(100, 500)]
    public int MaxEntries { get; set; }

    [IterationSetup]
    public void IterationSetup()
    {
        _cache = new MemoryCacheProvider(_loggerMock.Object, new MemoryCacheOptions
        {
            MaxEntries = MaxEntries
        });
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _cache.Dispose();
    }

    [Benchmark(Description = "LRU Eviction - Add entries beyond capacity")]
    public void LruEviction()
    {
        for (int i = 0; i < MaxEntries * 2; i++)
        {
            _cache.Set($"key{i}", $"value{i}");
        }
    }

    [Benchmark(Description = "Compact - Remove expired entries")]
    public void CompactExpired()
    {
        // Add some expired entries
        for (int i = 0; i < MaxEntries; i++)
        {
            var options = new CacheOptions
            {
                Expiration = TimeSpan.FromMilliseconds(-1)
            };
            _cache.Set($"expired_key{i}", $"value{i}", options);
        }

        _cache.Compact();
    }
}
