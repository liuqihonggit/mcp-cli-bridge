namespace MemoryCli.Services;

internal sealed class StoreBackendInfo
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool IsSupported { get; init; }
}

internal interface IStoreProvider
{
    StoreBackendInfo Backend { get; }
    IKnowledgeGraphStore CreateStore(MemoryOptions options);
}

internal interface IKnowledgeGraphStore : IDisposable
{
    Task<OperationResult<List<KnowledgeGraphEntity>>> LoadEntitiesAsync();
    Task<OperationResult<List<KnowledgeGraphRelation>>> LoadRelationsAsync();
    Task<OperationResult<object>> AppendEntityAsync(KnowledgeGraphEntity entity);
    Task<OperationResult<object>> AppendRelationAsync(KnowledgeGraphRelation relation);
    Task<OperationResult<object>> SaveEntitiesAsync(List<KnowledgeGraphEntity> entities);
    Task<OperationResult<object>> SaveRelationsAsync(List<KnowledgeGraphRelation> relations);
}

internal sealed class JsonlStoreProvider : IStoreProvider
{
    public StoreBackendInfo Backend => new()
    {
        Name = "jsonl",
        DisplayName = "JSON Lines",
        IsSupported = true
    };

    public IKnowledgeGraphStore CreateStore(MemoryOptions options) => new MemoryIoService(options);
}

internal sealed class UnsupportedStoreProvider : IStoreProvider
{
    public StoreBackendInfo Backend { get; }

    public UnsupportedStoreProvider(string name, string displayName)
    {
        Backend = new StoreBackendInfo
        {
            Name = name,
            DisplayName = displayName,
            IsSupported = false
        };
    }

    public IKnowledgeGraphStore CreateStore(MemoryOptions options) =>
        throw new NotSupportedException($"Storage backend '{Backend.DisplayName}' is not supported.");
}

internal static class StoreProviderRegistry
{
    private static readonly Dictionary<string, Func<IStoreProvider>> s_providers = new()
    {
        { "jsonl", static () => new JsonlStoreProvider() },
        { "json", static () => new JsonlStoreProvider() },
        { "jsonlines", static () => new JsonlStoreProvider() },
    };

    private static readonly Dictionary<string, (string Name, string DisplayName)> s_knownBackends = new()
    {
        { "sqlite", ("sqlite", "SQLite") },
        { "redis", ("redis", "Redis") },
        { "mongodb", ("mongodb", "MongoDB") },
        { "postgres", ("postgres", "PostgreSQL") },
        { "mysql", ("mysql", "MySQL") },
        { "leveldb", ("leveldb", "LevelDB") },
        { "rocksdb", ("rocksdb", "RocksDB") },
    };

    public static IStoreProvider GetProvider(string? backend)
    {
        if (string.IsNullOrWhiteSpace(backend))
            return new JsonlStoreProvider();

        var normalized = backend.Trim().ToLowerInvariant();

        if (s_providers.TryGetValue(normalized, out var factory))
            return factory();

        if (s_knownBackends.TryGetValue(normalized, out var backendInfo))
        {
            if (s_providers.TryGetValue(backendInfo.Name, out var extFactory))
                return extFactory();

            return new UnsupportedStoreProvider(backendInfo.Name, backendInfo.DisplayName);
        }

        return new UnsupportedStoreProvider(normalized, normalized);
    }

    public static List<string> GetSupportedBackends()
    {
        return s_providers.Keys.ToList();
    }

    public static void Register(string key, Func<IStoreProvider> factory)
    {
        s_providers[key.ToLowerInvariant()] = factory;
    }

    internal static void Unregister(string key)
    {
        s_providers.Remove(key.ToLowerInvariant());
    }
}
