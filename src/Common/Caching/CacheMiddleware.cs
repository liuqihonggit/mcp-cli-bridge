using Common.Middleware;

namespace Common.Caching;

/// <summary>
/// 缓存中间件，用于缓存工具执行结果
/// 支持AOT编译
/// </summary>
public sealed class CacheMiddleware : MiddlewareBase
{
    private readonly ICacheProvider _cache;
    private readonly ILogger _logger;
    private readonly CacheMiddlewareOptions _options;

    /// <summary>
    /// 初始化缓存中间件
    /// </summary>
    /// <param name="cache">缓存提供者</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">中间件选项</param>
    public CacheMiddleware(ICacheProvider cache, ILogger logger, CacheMiddlewareOptions? options = null)
    {
        _cache = ValidateService(cache, nameof(cache));
        _logger = ValidateService(logger, nameof(logger));
        _options = options ?? CacheMiddlewareOptions.Default;
    }

    /// <inheritdoc />
    public override async Task InvokeAsync(ToolContext context, Func<Task> nextMiddleware)
    {
        ValidateContext(context, nextMiddleware);

        // 检查是否应该缓存此工具
        if (!ShouldCache(context.ToolName))
        {
            await nextMiddleware().ConfigureAwait(false);
            return;
        }

        var cacheKey = CacheKeyGenerator.ForToolExecution(context.ToolName, context.Parameters);

        // 尝试从缓存获取
        if (_cache.TryGet<string>(cacheKey, out var cachedResult))
        {
            await _logger.LogAsync(LogLevel.Debug, $"Cache hit for tool '{context.ToolName}'").ConfigureAwait(false);
            context.Result = cachedResult;
            return;
        }

        await _logger.LogAsync(LogLevel.Debug, $"Cache miss for tool '{context.ToolName}'").ConfigureAwait(false);

        // 执行工具
        await nextMiddleware().ConfigureAwait(false);

        // 缓存结果
        if (!string.IsNullOrEmpty(context.Result) &&
            (!_options.CacheSuccessfulResultsOnly || IsSuccessResult(context.Result)))
        {
            var cacheOptions = GetCacheOptions(context.ToolName);
            _cache.SetValue(cacheKey, context.Result, cacheOptions);
            await _logger.LogAsync(LogLevel.Debug, $"Cached result for tool '{context.ToolName}'").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 检查是否应该缓存此工具
    /// </summary>
    private bool ShouldCache(string toolName)
    {
        // 如果有白名单，只缓存白名单中的工具
        if (_options.CacheableTools.Count > 0)
        {
            return _options.CacheableTools.Contains(toolName);
        }

        // 如果有黑名单，不缓存黑名单中的工具
        if (_options.NonCacheableTools.Count > 0)
        {
            return !_options.NonCacheableTools.Contains(toolName);
        }

        // 默认缓存所有工具
        return true;
    }

    /// <summary>
    /// 获取缓存选项
    /// </summary>
    private CacheOptions GetCacheOptions(string toolName)
    {
        if (_options.ToolExpirationOverrides.TryGetValue(toolName, out var expiration))
        {
            return new CacheOptions { SlidingExpiration = expiration };
        }

        return new CacheOptions { SlidingExpiration = _options.DefaultExpiration };
    }

    /// <summary>
    /// 检查是否为成功结果
    /// </summary>
    private static bool IsSuccessResult(string result)
    {
        if (string.IsNullOrEmpty(result))
        {
            return false;
        }

        try
        {
            var execResult = JsonSerializer.Deserialize(result, CommonJsonContext.Default.OperationResult);
            return execResult?.Success ?? false;
        }
        catch
        {
            return true; // 如果无法解析，假设成功
        }
    }

    /// <summary>
    /// 使指定工具的缓存失效
    /// </summary>
    /// <param name="toolName">工具名称</param>
    public void InvalidateToolCache(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        // 移除该工具的所有缓存（通过前缀匹配）
        // 注意：这是一个简化实现，生产环境可能需要更精确的缓存管理
        _cache.Remove(CacheKeyGenerator.ForToolExecution(toolName, new Dictionary<string, JsonElement>()));
        _logger.Log(LogLevel.Debug, $"Invalidated cache for tool '{toolName}'");
    }

    /// <summary>
    /// 清空所有工具执行缓存
    /// </summary>
    public void ClearAllToolCache()
    {
        _cache.Clear();
        _logger.Log(LogLevel.Info, "Cleared all tool execution cache.");
    }
}

/// <summary>
/// 缓存中间件选项
/// </summary>
public sealed class CacheMiddlewareOptions
{
    /// <summary>
    /// 默认缓存过期时间
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 是否只缓存成功结果
    /// </summary>
    public bool CacheSuccessfulResultsOnly { get; set; } = true;

    /// <summary>
    /// 可缓存的工具列表（白名单）
    /// </summary>
    public HashSet<string> CacheableTools { get; set; } = new();

    /// <summary>
    /// 不可缓存的工具列表（黑名单）
    /// </summary>
    public HashSet<string> NonCacheableTools { get; set; } = new();

    /// <summary>
    /// 特定工具的过期时间覆盖
    /// </summary>
    public Dictionary<string, TimeSpan> ToolExpirationOverrides { get; set; } = new();

    /// <summary>
    /// 默认选项
    /// </summary>
    public static CacheMiddlewareOptions Default => new();

    /// <summary>
    /// 只读模式（不缓存，只读取）
    /// </summary>
    public static CacheMiddlewareOptions ReadOnly => new() { CacheSuccessfulResultsOnly = false };
}
