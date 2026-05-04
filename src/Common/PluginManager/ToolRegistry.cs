namespace Common.PluginManager;

/// <summary>
/// 工具注册中心实现 — 渐进式发现架构
/// 启动时不预加载CLI内部工具，只在 tool_describe / tool_execute 时按需获取
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

    private sealed class ToolRegistration
    {
        public required IToolMetadata Metadata { get; init; }
        public required IToolProvider Provider { get; init; }
        public required string ProviderName { get; init; }
    }

    public ToolRegistry(ILogger logger, ICacheProvider? cache = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 只注册 provider，不预加载其内部工具。
    /// CLI 内部工具通过 tool_describe / tool_execute 按需渐进式获取。
    /// </remarks>
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

            _providers[providerName] = provider;
            _toolListCacheInvalidated = true;
            _logger.Log(LogLevel.Info, $"Plugin '{providerName}' registered successfully.");
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
            _logger.Log(LogLevel.Info, $"Plugin '{providerName}' unregistered successfully.");

            return true;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 返回已被访问/缓存过的工具元数据。
    /// 不触发任何 CLI 调用，不预加载未访问的工具。
    /// </remarks>
    public IReadOnlyList<IToolMetadata> GetAllTools()
    {
        if (_cache is not null && !_toolListCacheInvalidated)
        {
            var cacheKey = CacheKeyGenerator.ForToolList();
            if (_cache.TryGet<IReadOnlyList<IToolMetadata>>(cacheKey, out var cachedTools))
            {
                return cachedTools!;
            }
        }

        var tools = _toolRegistry.Values
            .Select(reg => reg.Metadata)
            .ToList()
            .AsReadOnly();

        if (_cache is not null)
        {
            var cacheKey = CacheKeyGenerator.ForToolList();
            _cache.SetValue(cacheKey, tools, CacheOptions.LongLived);
            _toolListCacheInvalidated = false;
        }

        return tools;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 按需获取指定插件的完整命令列表（渐进式发现入口）。
    /// 会触发 CLI 的 list_commands 调用。
    /// </remarks>
    public async Task<IReadOnlyList<IToolMetadata>> GetPluginCommandsAsync(string pluginName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginName);

        if (!_providers.TryGetValue(pluginName, out var provider))
        {
            _logger.Log(LogLevel.Warn, $"Provider '{pluginName}' not found.");
            return [];
        }

        var commands = provider.GetAvailableTools();

        foreach (var command in commands)
        {
            EnsureToolRegistered(command, provider, pluginName);
        }

        return commands;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 三级查找：缓存 → 已注册字典 → 按需遍历 provider
    /// </remarks>
    public bool TryGetTool(string toolName, [NotNullWhen(true)] out IToolMetadata? metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (_cache is not null)
        {
            var cacheKey = CacheKeyGenerator.ForToolMetadata(toolName);
            if (_cache.TryGet<IToolMetadata>(cacheKey, out var cachedMetadata) && cachedMetadata is not null)
            {
                metadata = cachedMetadata;
                return true;
            }
        }

        if (_toolRegistry.TryGetValue(toolName, out var registration))
        {
            metadata = registration.Metadata;
            CacheToolMetadata(toolName, metadata);
            return true;
        }

        metadata = null;
        return false;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 两级执行：已注册字典 → 按需遍历 provider
    /// </remarks>
    public async Task<OperationResult> ExecuteToolAsync(
        string toolName,
        IReadOnlyDictionary<string, JsonElement> parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(parameters);

        if (_toolRegistry.TryGetValue(toolName, out var registration))
        {
            return await ExecuteViaProviderAsync(registration.Provider, registration.ProviderName, toolName, parameters, cancellationToken);
        }

        foreach (var kvp in _providers)
        {
            var providerName = kvp.Key;
            var provider = kvp.Value;

            var commands = provider.GetAvailableTools();
            var matched = commands.FirstOrDefault(c => c.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));

            if (matched is not null)
            {
                EnsureToolRegistered(matched, provider, providerName);
                return await ExecuteViaProviderAsync(provider, providerName, toolName, parameters, cancellationToken);
            }
        }

        _logger.Log(LogLevel.Error, $"Tool '{toolName}' not found in any provider.");
        return OperationResultFactoryNonGeneric.CliFailure($"Tool '{toolName}' not found.");
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetProviderNames()
    {
        return _providers.Keys.ToList().AsReadOnly();
    }

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

    public int ToolCount => _toolRegistry.Count;
    public int ProviderCount => _providers.Count;

    private void EnsureToolRegistered(IToolMetadata metadata, IToolProvider provider, string providerName)
    {
        if (_toolRegistry.ContainsKey(metadata.Name)) return;

        _toolRegistry.TryAdd(metadata.Name, new ToolRegistration
        {
            Metadata = metadata,
            Provider = provider,
            ProviderName = providerName
        });

        _toolListCacheInvalidated = true;
        CacheToolMetadata(metadata.Name, metadata);
    }

    private void CacheToolMetadata(string toolName, IToolMetadata metadata)
    {
        if (_cache is not null)
        {
            var cacheKey = CacheKeyGenerator.ForToolMetadata(toolName);
            _cache.SetValue(cacheKey, metadata, CacheOptions.LongLived);
        }
    }

    private async Task<OperationResult> ExecuteViaProviderAsync(
        IToolProvider provider,
        string providerName,
        string toolName,
        IReadOnlyDictionary<string, JsonElement> parameters,
        CancellationToken cancellationToken)
    {
        _logger.Log(LogLevel.Debug, $"Executing tool '{toolName}' via provider '{providerName}'.");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await provider.ExecuteAsync(toolName, parameters, cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();

            _logger.Log(LogLevel.Debug,
                $"Tool '{toolName}' executed by '{providerName}' in {stopwatch.ElapsedMilliseconds}ms. Success: {result.Success}");

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
            _logger.Log(LogLevel.Error, ex, $"Tool '{toolName}' execution failed: {ex.Message}");
            return OperationResultFactoryNonGeneric.FromException(ex, stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
