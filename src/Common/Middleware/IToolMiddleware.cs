namespace Common.Middleware;

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
