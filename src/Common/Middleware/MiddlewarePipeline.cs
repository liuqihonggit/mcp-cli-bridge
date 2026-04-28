namespace Common.Middleware;

using IServiceProvider = Common.Contracts.IoC.IServiceProvider;

/// <summary>
/// 中间件管道实现，管理中间件链的构建和执行
/// </summary>
public sealed class MiddlewarePipeline : IMiddlewarePipeline
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<MiddlewareRegistration> _middlewares = [];

    /// <summary>
    /// 中间件注册信息
    /// </summary>
    private readonly record struct MiddlewareRegistration(
        Type? MiddlewareType,
        Func<ToolContext, Func<Task>, Task>? MiddlewareDelegate);

    /// <summary>
    /// 初始化中间件管道
    /// </summary>
    /// <param name="serviceProvider">服务提供器</param>
    public MiddlewarePipeline(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public void Use<TMiddleware>() where TMiddleware : IToolMiddleware
    {
        _middlewares.Add(new MiddlewareRegistration(typeof(TMiddleware), null));
    }

    /// <inheritdoc />
    public void Use(Func<ToolContext, Func<Task>, Task> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        _middlewares.Add(new MiddlewareRegistration(null, middleware));
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(ToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 设置上下文的服务提供器
        context.ServiceProvider = _serviceProvider;

        // 构建中间件链
        var index = 0;

        Func<Task> BuildNext()
        {
            var currentIndex = index;
            if (currentIndex >= _middlewares.Count)
            {
                return () => Task.CompletedTask;
            }

            index++;
            var registration = _middlewares[currentIndex];

            return () => ExecuteMiddlewareAsync(registration, context, BuildNext);
        }

        // 从第一个中间件开始执行
        var pipeline = BuildNext();
        await pipeline();
    }

    /// <summary>
    /// 执行单个中间件
    /// </summary>
    private static async Task ExecuteMiddlewareAsync(
        MiddlewareRegistration registration,
        ToolContext context,
        Func<Func<Task>> buildNext)
    {
        // 检查是否已取消
        if (context.IsCancelled)
        {
            return;
        }

        var next = buildNext();

        if (registration.MiddlewareDelegate is not null)
        {
            // 执行委托中间件
            await registration.MiddlewareDelegate(context, next);
        }
        else if (registration.MiddlewareType is not null)
        {
            // 执行类型化中间件
            var service = context.ServiceProvider.GetService(registration.MiddlewareType);
            if (service is null)
            {
                throw new InvalidOperationException(
                    $"无法解析中间件类型: {registration.MiddlewareType.FullName}");
            }

            var middleware = (IToolMiddleware)service;
            await middleware.InvokeAsync(context, next);
        }
    }
}

/// <summary>
/// 中间件管道构建器扩展方法
/// </summary>
public static class MiddlewarePipelineExtensions
{
    /// <summary>
    /// 添加日志中间件
    /// </summary>
    /// <typeparam name="TMiddleware">中间件类型</typeparam>
    /// <param name="pipeline">中间件管道</param>
    /// <returns>中间件管道（链式调用）</returns>
    public static IMiddlewarePipeline Use<TMiddleware>(this IMiddlewarePipeline pipeline)
        where TMiddleware : IToolMiddleware
    {
        pipeline.Use<TMiddleware>();
        return pipeline;
    }

    /// <summary>
    /// 添加基于委托的中间件
    /// </summary>
    /// <param name="pipeline">中间件管道</param>
    /// <param name="middleware">中间件委托</param>
    /// <returns>中间件管道（链式调用）</returns>
    public static IMiddlewarePipeline Use(
        this IMiddlewarePipeline pipeline,
        Func<ToolContext, Func<Task>, Task> middleware)
    {
        pipeline.Use(middleware);
        return pipeline;
    }
}
