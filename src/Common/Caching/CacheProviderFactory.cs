namespace Common.Caching;

public static class CacheProviderFactory
{
    private static readonly Dictionary<string, Func<ILogger, MemoryCacheOptions?, ICacheProvider>> s_providers = new()
    {
        { "memory", static (logger, options) => new MemoryCacheProvider(logger, options) },
        { "inmemory", static (logger, options) => new MemoryCacheProvider(logger, options) },
        { "volatile", static (logger, options) => new MemoryCacheProvider(logger, options) },
    };

    private static readonly Dictionary<string, string> s_knownProviders = new()
    {
        { "redis", "Redis" },
        { "memcached", "Memcached" },
        { "sqlite", "SQLite" },
        { "file", "File-based" },
        { "distributed", "Distributed" },
    };

    public static ICacheProvider Create(ILogger logger, string? providerName = null, MemoryCacheOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return new MemoryCacheProvider(logger, options);

        var normalized = providerName.Trim().ToLowerInvariant();

        if (s_providers.TryGetValue(normalized, out var factory))
            return factory(logger, options);

        if (s_knownProviders.TryGetValue(normalized, out var displayName))
            throw new NotSupportedException($"Cache provider '{displayName}' is not supported. Supported providers: {string.Join(", ", s_providers.Keys)}");

        throw new NotSupportedException($"Cache provider '{providerName}' is not supported. Supported providers: {string.Join(", ", s_providers.Keys)}");
    }

    public static List<string> GetSupportedProviders() => s_providers.Keys.ToList();

    public static void Register(string key, Func<ILogger, MemoryCacheOptions?, ICacheProvider> factory)
    {
        s_providers[key.ToLowerInvariant()] = factory;
    }
}
