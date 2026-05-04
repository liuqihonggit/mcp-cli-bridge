namespace Common.Contracts.Middleware;

/// <summary>
/// 工具中间件接口，用于在工具执行管道中插入自定义逻辑
/// </summary>
public interface IToolMiddleware
{
    /// <summary>
    /// 执行中间件逻辑
    /// </summary>
    /// <param name="context">工具执行上下文</param>
    /// <param name="next">下一个中间件的委托</param>
    /// <returns>异步任务</returns>
    Task InvokeAsync(ToolContext context, Func<Task> next);
}

/// <summary>
/// 中间件管道接口，用于构建和执行中间件链
/// </summary>
public interface IMiddlewarePipeline
{
    /// <summary>
    /// 注册类型化的中间件
    /// </summary>
    /// <typeparam name="TMiddleware">中间件类型，必须实现 <see cref="IToolMiddleware"/></typeparam>
    void Use<TMiddleware>() where TMiddleware : IToolMiddleware;

    /// <summary>
    /// 注册基于委托的中间件
    /// </summary>
    /// <param name="middleware">中间件委托</param>
    void Use(Func<ToolContext, Func<Task>, Task> middleware);

    /// <summary>
    /// 执行中间件管道
    /// </summary>
    /// <param name="context">工具执行上下文</param>
    /// <returns>异步任务</returns>
    Task ExecuteAsync(ToolContext context);
}
