namespace Common.Plugins;

/// <summary>
/// 工具注册中心实现，管理工具提供者的注册和工具发现
/// 线程安全，支持AOT编译，支持缓存
/// </summary>
public sealed class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, IToolProvider> _providers = new();
    private readonly ConcurrentDictionary<string, ToolRegistration> _toolRegistry = new();
    private readonly ILogger _logger;
    private readonly ICacheProvider? _cache;
    private readonly Lock _registrationLock = new();
    private bool _toolListCacheInvalidated = true;

    /// <summary>
    /// 工具注册信息，包含工具元数据和所属提供者
    /// </summary>
    private sealed class ToolRegistration
    {
        public required IToolMetadata Metadata { get; init; }
        public required IToolProvider Provider { get; init; }
        public required string ProviderName { get; init; }
    }

    /// <summary>
    /// 初始化工具注册中心
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="cache">缓存提供者（可选）</param>
    public ToolRegistry(ILogger logger, ICacheProvider? cache = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache;
    }

    /// <inheritdoc />
    public void RegisterProvider(IToolProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var providerName = provider.ProviderName;

        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name cannot be null or empty.", nameof(provider));
        }

        lock (_registrationLock)
        {
            if (_providers.ContainsKey(providerName))
            {
                _logger.Log(LogLevel.Warn, $"Provider '{providerName}' is already registered. Skipping.");
                return;
            }

            var tools = provider.GetAvailableTools();
            var registeredCount = 0;
            var conflictCount = 0;

            foreach (var tool in tools)
            {
                var toolName = tool.Name;

                if (_toolRegistry.TryAdd(toolName, new ToolRegistration
                {
                    Metadata = tool,
                    Provider = provider,
                    ProviderName = providerName
                }))
                {
                    registeredCount++;
                }
                else
                {
                    conflictCount++;
                    _logger.Log(LogLevel.Warn,
                        $"Tool '{toolName}' from provider '{providerName}' conflicts with existing registration. " +
                        $"Existing provider: '{_toolRegistry[toolName].ProviderName}'. Skipping.");
                }
            }

            _providers[providerName] = provider;
            _toolListCacheInvalidated = true;
            _logger.Log(LogLevel.Info,
                $"Provider '{providerName}' registered successfully. " +
                $"Tools: {registeredCount} registered, {conflictCount} conflicts skipped.");
        }
    }

    /// <inheritdoc />
    public bool UnregisterProvider(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        lock (_registrationLock)
        {
            if (!_providers.TryRemove(providerName, out _))
            {
                _logger.Log(LogLevel.Warn, $"Provider '{providerName}' is not registered.");
                return false;
            }

            var toolsToRemove = _toolRegistry
                .Where(kvp => kvp.Value.ProviderName == providerName)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var toolName in toolsToRemove)
            {
                _toolRegistry.TryRemove(toolName, out _);
            }

            _toolListCacheInvalidated = true;
            _logger.Log(LogLevel.Info,
                $"Provider '{providerName}' unregistered successfully. " +
                $"Removed {toolsToRemove.Count} tools.");

            return true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IToolMetadata> GetAllTools()
    {
        // 尝试从缓存获取
        if (_cache is not null && !_toolListCacheInvalidated)
        {
            var cacheKey = CacheKeyGenerator.ForToolList();
            if (_cache.TryGet<IReadOnlyList<IToolMetadata>>(cacheKey, out var cachedTools))
            {
                _logger.Log(LogLevel.Debug, "Tool list retrieved from cache.");
                return cachedTools!;
            }
        }

        var tools = _toolRegistry.Values
            .Select(reg => reg.Metadata)
            .ToList()
            .AsReadOnly();

        // 缓存结果
        if (_cache is not null)
        {
            var cacheKey = CacheKeyGenerator.ForToolList();
            _cache.Set(cacheKey, tools, CacheOptions.LongLived);
            _toolListCacheInvalidated = false;
            _logger.Log(LogLevel.Debug, "Tool list cached.");
        }

        return tools;
    }

    /// <inheritdoc />
    public bool TryGetTool(string toolName, [NotNullWhen(true)] out IToolMetadata? metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        // 尝试从缓存获取
        if (_cache is not null)
        {
            var cacheKey = CacheKeyGenerator.ForToolMetadata(toolName);
            if (_cache.TryGet<IToolMetadata>(cacheKey, out var cachedMetadata) && cachedMetadata is not null)
            {
                _logger.Log(LogLevel.Debug, $"Tool '{toolName}' metadata retrieved from cache.");
                metadata = cachedMetadata;
                return true;
            }
        }

        if (_toolRegistry.TryGetValue(toolName, out var registration))
        {
            metadata = registration.Metadata;

            // 缓存结果
            if (_cache is not null)
            {
                var cacheKey = CacheKeyGenerator.ForToolMetadata(toolName);
                _cache.Set(cacheKey, metadata, CacheOptions.LongLived);
                _logger.Log(LogLevel.Debug, $"Tool '{toolName}' metadata cached.");
            }

            return true;
        }

        metadata = null;
        return false;
    }

    /// <inheritdoc />
    public async Task<OperationResult> ExecuteToolAsync(
        string toolName,
        IReadOnlyDictionary<string, JsonElement> parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(parameters);

        if (!_toolRegistry.TryGetValue(toolName, out var registration))
        {
            _logger.Log(LogLevel.Error, $"Tool '{toolName}' not found in registry.");
            return OperationResultFactoryNonGeneric.CliFailure($"Tool '{toolName}' not found.");
        }

        var provider = registration.Provider;
        var providerName = registration.ProviderName;

        _logger.Log(LogLevel.Debug,
            $"Executing tool '{toolName}' via provider '{providerName}'.");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await provider.ExecuteAsync(toolName, parameters, cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();

            _logger.Log(LogLevel.Debug,
                $"Tool '{toolName}' executed by '{providerName}' in {stopwatch.ElapsedMilliseconds}ms. " +
                $"Success: {result.Success}");

            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.Log(LogLevel.Warn, $"Tool '{toolName}' execution was cancelled.");

            return OperationResultFactoryNonGeneric.Cancelled(stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Log(LogLevel.Error, ex,
                $"Tool '{toolName}' execution failed with error: {ex.Message}");

            return OperationResultFactoryNonGeneric.FromException(ex, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetProviderNames()
    {
        return _providers.Keys.ToList().AsReadOnly();
    }

    /// <summary>
    /// 获取指定工具的提供者名称
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="providerName">提供者名称（如果找到）</param>
    /// <returns>是否找到指定工具</returns>
    public bool TryGetProviderName(string toolName, [NotNullWhen(true)] out string? providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (_toolRegistry.TryGetValue(toolName, out var registration))
        {
            providerName = registration.ProviderName;
            return true;
        }

        providerName = null;
        return false;
    }

    /// <summary>
    /// 获取已注册的工具数量
    /// </summary>
    public int ToolCount => _toolRegistry.Count;

    /// <summary>
    /// 获取已注册的提供者数量
    /// </summary>
    public int ProviderCount => _providers.Count;
}
