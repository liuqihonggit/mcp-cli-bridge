namespace McpHost.Middleware;

using IServiceProvider = Common.Contracts.IoC.IServiceProvider;
using IMiddlewarePipeline = Common.Contracts.Middleware.IMiddlewarePipeline;

/// <summary>
/// 中间件配置
/// </summary>
public sealed class MiddlewareConfiguration
{
    /// <summary>
    /// 中间件名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 执行顺序（数字越小越先执行）
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; }

    /// <summary>
    /// 中间件特定配置
    /// </summary>
    [JsonPropertyName("options")]
    public Dictionary<string, object>? Options { get; set; }
}

/// <summary>
/// 中间件管道配置
/// </summary>
public sealed class MiddlewarePipelineConfiguration
{
    /// <summary>
    /// 中间件列表
    /// </summary>
    [JsonPropertyName("middlewares")]
    public List<MiddlewareConfiguration> Middlewares { get; set; } = [];

    /// <summary>
    /// 默认配置
    /// </summary>
    public static MiddlewarePipelineConfiguration Default => new()
    {
        Middlewares =
        [
            new MiddlewareConfiguration { Name = nameof(ExceptionHandlingMiddleware), Order = 0 },
            new MiddlewareConfiguration { Name = nameof(LoggingMiddleware), Order = 1 },
            new MiddlewareConfiguration { Name = nameof(Common.Security.Middleware.SecurityValidationMiddleware), Order = 2 },
            new MiddlewareConfiguration { Name = nameof(Common.Caching.CacheMiddleware), Order = 3 },
            new MiddlewareConfiguration { Name = nameof(ExecutionMiddleware), Order = 4 }
        ]
    };
}

/// <summary>
/// 中间件管道构建器
/// </summary>
public sealed class MiddlewarePipelineBuilder
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MiddlewarePipelineConfiguration _configuration;
    private readonly List<(string Name, int Order, bool Enabled)> _middlewares = [];

    public MiddlewarePipelineBuilder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _configuration = MiddlewarePipelineConfiguration.Default;
    }

    public MiddlewarePipelineBuilder(IServiceProvider serviceProvider, MiddlewarePipelineConfiguration configuration)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// 添加中间件
    /// </summary>
    public MiddlewarePipelineBuilder Use(string name, int order = 0, bool enabled = true)
    {
        _middlewares.Add((name, order, enabled));
        return this;
    }

    /// <summary>
    /// 使用默认中间件配置
    /// </summary>
    public MiddlewarePipelineBuilder UseDefault()
    {
        foreach (var middleware in _configuration.Middlewares)
        {
            _middlewares.Add((middleware.Name, middleware.Order, middleware.Enabled));
        }
        return this;
    }

    /// <summary>
    /// 构建中间件管道
    /// </summary>
    public IMiddlewarePipeline Build()
    {
        var pipeline = new Common.Middleware.MiddlewarePipeline(_serviceProvider);

        // 按顺序排序并添加启用的中间件
        var orderedMiddlewares = _middlewares
            .Where(m => m.Enabled)
            .OrderBy(m => m.Order)
            .ToList();

        foreach (var (name, _, _) in orderedMiddlewares)
        {
            AddMiddlewareByName(pipeline, name);
        }

        return pipeline;
    }

    private static void AddMiddlewareByName(Common.Middleware.MiddlewarePipeline pipeline, string name)
    {
        switch (name)
        {
            case nameof(ExceptionHandlingMiddleware):
                pipeline.Use<ExceptionHandlingMiddleware>();
                break;
            case nameof(LoggingMiddleware):
                pipeline.Use<LoggingMiddleware>();
                break;
            case nameof(Common.Security.Middleware.SecurityValidationMiddleware):
                pipeline.Use<Common.Security.Middleware.SecurityValidationMiddleware>();
                break;
            case nameof(Common.Caching.CacheMiddleware):
                pipeline.Use<Common.Caching.CacheMiddleware>();
                break;
            case nameof(ExecutionMiddleware):
                pipeline.Use<ExecutionMiddleware>();
                break;
            default:
                throw new InvalidOperationException($"未知的中间件: {name}");
        }
    }
}

/// <summary>
/// 中间件管道扩展方法
/// </summary>
public static class MiddlewarePipelineBuilderExtensions
{
    /// <summary>
    /// 使用异常处理中间件
    /// </summary>
    public static MiddlewarePipelineBuilder UseExceptionHandling(this MiddlewarePipelineBuilder builder, int order = 0)
    {
        return builder.Use(nameof(ExceptionHandlingMiddleware), order);
    }

    /// <summary>
    /// 使用日志中间件
    /// </summary>
    public static MiddlewarePipelineBuilder UseLogging(this MiddlewarePipelineBuilder builder, int order = 1)
    {
        return builder.Use(nameof(LoggingMiddleware), order);
    }

    /// <summary>
    /// 使用验证中间件
    /// </summary>
    public static MiddlewarePipelineBuilder UseValidation(this MiddlewarePipelineBuilder builder, int order = 2)
    {
        return builder.Use(nameof(Common.Security.Middleware.SecurityValidationMiddleware), order);
    }

    /// <summary>
    /// 使用缓存中间件
    /// </summary>
    public static MiddlewarePipelineBuilder UseCache(this MiddlewarePipelineBuilder builder, int order = 3)
    {
        return builder.Use(nameof(Common.Caching.CacheMiddleware), order);
    }

    /// <summary>
    /// 使用执行中间件
    /// </summary>
    public static MiddlewarePipelineBuilder UseExecution(this MiddlewarePipelineBuilder builder, int order = 4)
    {
        return builder.Use(nameof(ExecutionMiddleware), order);
    }
}
