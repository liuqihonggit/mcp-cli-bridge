#pragma warning disable VSTHRD002

namespace Common.Reflection;

/// <summary>
/// 表达式树编译的方法调用器，提供接近直接调用的性能
/// 线程安全，支持AOT编译
/// </summary>
public sealed class ExpressionMethodInvoker : IMethodInvoker
{
    private readonly Func<object?[], object?>? _arrayInvoker;
    private readonly Func<object?, object?[], object?>? _instanceArrayInvoker;
    private readonly Type _returnType;
    private readonly bool _isVoid;

    /// <inheritdoc />
    public MethodInfo Method { get; }

    /// <inheritdoc />
    public int ParameterCount { get; }

    /// <inheritdoc />
    public bool IsAsync { get; }

    /// <summary>
    /// 创建表达式树编译的方法调用器
    /// </summary>
    /// <param name="method">要编译的方法</param>
    public ExpressionMethodInvoker(MethodInfo method)
    {
        Method = method ?? throw new ArgumentNullException(nameof(method));
        ParameterCount = method.GetParameters().Length;
        _returnType = method.ReturnType;
        _isVoid = _returnType == typeof(void);
        IsAsync = typeof(Task).IsAssignableFrom(_returnType);

        // 编译方法调用
        if (method.IsStatic)
        {
            _arrayInvoker = CompileStaticMethod(method);
            _instanceArrayInvoker = null;
        }
        else
        {
            _instanceArrayInvoker = CompileInstanceMethod(method);
            _arrayInvoker = null;
        }
    }

    /// <inheritdoc />
    public object? Invoke(object? instance, object?[]? arguments)
    {
        if (!Method.IsStatic && instance == null)
        {
            throw new ArgumentNullException(nameof(instance), $"Instance method '{Method.Name}' requires a non-null instance.");
        }

        var args = arguments ?? Array.Empty<object?>();

        if (args.Length != ParameterCount)
        {
            throw new ArgumentException(
                $"Parameter count mismatch. Expected {ParameterCount}, got {args.Length}.",
                nameof(arguments));
        }

        if (Method.IsStatic)
        {
            return _arrayInvoker!(args);
        }

        return _instanceArrayInvoker!(instance, args);
    }

    /// <inheritdoc />
    public async Task<object?> InvokeAsync(object? instance, object?[]? arguments, CancellationToken cancellationToken = default)
    {
        var result = Invoke(instance, arguments);

        if (result is Task task)
        {
            await task.ConfigureAwait(false);

            // 返回Task<T>的结果
            return ExtractTaskResult(task);
        }

        return result;
    }

    /// <summary>
    /// 提取Task的结果 - 使用编译时泛型方法避免AOT反射问题
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Task结果提取是AOT安全的")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Task<TResult>.Result是框架类型")]
    [UnconditionalSuppressMessage("Trimming", "IL2075:DynamicallyAccessedMembers", Justification = "Task类型由运行时保留")]
    private static object? ExtractTaskResult(Task task)
    {
        if (task.IsFaulted)
        {
            throw task.Exception?.InnerException ?? new InvalidOperationException("Task faulted", task.Exception);
        }

        if (task.IsCanceled)
        {
            throw new OperationCanceledException("Task was canceled");
        }

        var taskType = task.GetType();
        if (!taskType.IsGenericType)
        {
            return null;
        }

        var genericArgs = taskType.GetGenericArguments();
        if (genericArgs.Length == 0)
        {
            return null;
        }

        var resultType = genericArgs[0];

        return ExtractTaskResultCore(task, resultType);
    }

    [UnconditionalSuppressMessage("AOT", "IL2075", Justification = "Task类型由运行时保留")]
    private static object? ExtractTaskResultCore(Task task, Type resultType)
    {
        if (resultType == typeof(string))
            return ((Task<string>)task).Result;
        if (resultType == typeof(int))
            return ((Task<int>)task).Result;
        if (resultType == typeof(long))
            return ((Task<long>)task).Result;
        if (resultType == typeof(bool))
            return ((Task<bool>)task).Result;
        if (resultType == typeof(double))
            return ((Task<double>)task).Result;
        if (resultType == typeof(float))
            return ((Task<float>)task).Result;
        if (resultType == typeof(decimal))
            return ((Task<decimal>)task).Result;
        if (resultType == typeof(DateTime))
            return ((Task<DateTime>)task).Result;
        if (resultType == typeof(DateTimeOffset))
            return ((Task<DateTimeOffset>)task).Result;
        if (resultType == typeof(Guid))
            return ((Task<Guid>)task).Result;
        if (resultType == typeof(byte[]))
            return ((Task<byte[]>)task).Result;
        if (resultType == typeof(JsonElement))
            return ((Task<JsonElement>)task).Result;

        var taskType = task.GetType();
        var resultProperty = taskType.GetProperty("Result");
        return resultProperty?.GetValue(task);
    }

    /// <summary>
    /// 编译静态方法调用
    /// </summary>
    private static Func<object?[], object?> CompileStaticMethod(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var argsParam = Expression.Parameter(typeof(object?[]), "args");

        var parameterExpressions = new Expression[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            var indexExpr = Expression.Constant(i);
            var arrayAccess = Expression.ArrayIndex(argsParam, indexExpr);

            parameterExpressions[i] = Expression.Convert(arrayAccess, paramType);
        }

        var callExpr = Expression.Call(method, parameterExpressions);

        Expression finalExpr = method.ReturnType == typeof(void)
            ? Expression.Block(callExpr, Expression.Default(typeof(object)))
            : Expression.Convert(callExpr, typeof(object));

        var lambda = Expression.Lambda<Func<object?[], object?>>(finalExpr, argsParam);
        return lambda.Compile();
    }

    /// <summary>
    /// 编译实例方法调用
    /// </summary>
    private static Func<object?, object?[], object?> CompileInstanceMethod(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var instanceParam = Expression.Parameter(typeof(object), "instance");
        var argsParam = Expression.Parameter(typeof(object?[]), "args");

        var castInstance = Expression.Convert(instanceParam, method.DeclaringType!);

        var parameterExpressions = new Expression[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            var indexExpr = Expression.Constant(i);
            var arrayAccess = Expression.ArrayIndex(argsParam, indexExpr);

            parameterExpressions[i] = Expression.Convert(arrayAccess, paramType);
        }

        var callExpr = Expression.Call(castInstance, method, parameterExpressions);

        Expression finalExpr = method.ReturnType == typeof(void)
            ? Expression.Block(callExpr, Expression.Default(typeof(object)))
            : Expression.Convert(callExpr, typeof(object));

        var lambda = Expression.Lambda<Func<object?, object?[], object?>>(finalExpr, instanceParam, argsParam);
        return lambda.Compile();
    }
}

/// <summary>
/// 方法调用器工厂实现，使用表达式树编译并缓存方法调用器
/// </summary>
public sealed class MethodInvokerFactory : IMethodInvokerFactory
{
    private readonly ConcurrentDictionary<MethodInfo, IMethodInvoker> _cache = new();

    /// <inheritdoc />
    public IMethodInvoker GetOrCreate(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return _cache.GetOrAdd(method, m => new ExpressionMethodInvoker(m));
    }

    /// <inheritdoc />
    public bool TryGet(MethodInfo method, [NotNullWhen(true)] out IMethodInvoker? invoker)
    {
        ArgumentNullException.ThrowIfNull(method);

        return _cache.TryGetValue(method, out invoker);
    }

    /// <inheritdoc />
    public void Clear()
    {
        _cache.Clear();
    }

    /// <inheritdoc />
    public int CachedCount => _cache.Count;
}

/// <summary>
/// 方法调用器缓存键，用于精确匹配方法
/// </summary>
internal sealed class MethodCacheKey : IEquatable<MethodCacheKey>
{
    private readonly MethodInfo _method;
    private readonly int _hashCode;

    public MethodCacheKey(MethodInfo method)
    {
        _method = method;
        _hashCode = CalculateHashCode(method);
    }

    public bool Equals(MethodCacheKey? other)
    {
        if (other is null) return false;
        return _method == other._method;
    }

    public override bool Equals(object? obj) => Equals(obj as MethodCacheKey);

    public override int GetHashCode() => _hashCode;

    private static int CalculateHashCode(MethodInfo method)
    {
        unchecked
        {
            var hash = method.DeclaringType?.GetHashCode() ?? 0;
            hash = (hash * 397) ^ method.Name.GetHashCode();
            hash = (hash * 397) ^ method.GetParameters().Length;
            return hash;
        }
    }

    public static bool operator ==(MethodCacheKey? left, MethodCacheKey? right) => Equals(left, right);

    public static bool operator !=(MethodCacheKey? left, MethodCacheKey? right) => !Equals(left, right);
}
