namespace Common.Contracts;

/// <summary>
/// 方法调用器接口，用于高性能方法调用
/// 支持同步和异步方法，使用表达式树编译实现接近直接调用的性能
/// </summary>
public interface IMethodInvoker
{
    /// <summary>
    /// 方法信息
    /// </summary>
    MethodInfo Method { get; }

    /// <summary>
    /// 方法参数数量
    /// </summary>
    int ParameterCount { get; }

    /// <summary>
    /// 是否为异步方法（返回Task或Task&lt;T&gt;）
    /// </summary>
    bool IsAsync { get; }

    /// <summary>
    /// 调用方法并返回结果
    /// </summary>
    /// <param name="instance">方法所属实例（静态方法为null）</param>
    /// <param name="arguments">方法参数</param>
    /// <returns>方法返回值（异步方法返回Task结果）</returns>
    object? Invoke(object? instance, object?[]? arguments);

    /// <summary>
    /// 异步调用方法
    /// </summary>
    /// <param name="instance">方法所属实例（静态方法为null）</param>
    /// <param name="arguments">方法参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>方法返回值</returns>
    Task<object?> InvokeAsync(object? instance, object?[]? arguments, CancellationToken cancellationToken = default);
}

/// <summary>
/// 方法调用器工厂接口，用于创建和缓存方法调用器
/// </summary>
public interface IMethodInvokerFactory
{
    /// <summary>
    /// 获取或创建方法调用器
    /// </summary>
    /// <param name="method">方法信息</param>
    /// <returns>编译后的方法调用器</returns>
    IMethodInvoker GetOrCreate(MethodInfo method);

    /// <summary>
    /// 尝试获取已缓存的方法调用器
    /// </summary>
    /// <param name="method">方法信息</param>
    /// <param name="invoker">方法调用器（如果找到）</param>
    /// <returns>是否找到</returns>
    bool TryGet(MethodInfo method, [NotNullWhen(true)] out IMethodInvoker? invoker);

    /// <summary>
    /// 清除所有缓存的调用器
    /// </summary>
    void Clear();

    /// <summary>
    /// 获取缓存的调用器数量
    /// </summary>
    int CachedCount { get; }
}
