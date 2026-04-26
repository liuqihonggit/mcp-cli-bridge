namespace Benchmarks;

/// <summary>
/// 反射优化性能基准测试
/// 对比反射调用与表达式树编译调用的性能差异
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ReflectionOptimizationBenchmark
{
    private TestService _service = null!;
    private MethodInfo _methodNoArgs = null!;
    private MethodInfo _methodWithArgs = null!;
    private MethodInfo _methodAsync = null!;
    private MethodInfo _methodComplex = null!;
    private IMethodInvoker _invokerNoArgs = null!;
    private IMethodInvoker _invokerWithArgs = null!;
    private IMethodInvoker _invokerAsync = null!;
    private IMethodInvoker _invokerComplex = null!;
    private object?[] _args = null!;
    private object?[] _complexArgs = null!;
    private MethodInvokerFactory _factory = null!;

    [GlobalSetup]
    public void Setup()
    {
        _service = new TestService();
        var type = typeof(TestService);

        _methodNoArgs = type.GetMethod(nameof(TestService.NoArgsMethod))!;
        _methodWithArgs = type.GetMethod(nameof(TestService.WithArgsMethod))!;
        _methodAsync = type.GetMethod(nameof(TestService.AsyncMethod))!;
        _methodComplex = type.GetMethod(nameof(TestService.ComplexMethod))!;

        _factory = new MethodInvokerFactory();
        _invokerNoArgs = _factory.GetOrCreate(_methodNoArgs);
        _invokerWithArgs = _factory.GetOrCreate(_methodWithArgs);
        _invokerAsync = _factory.GetOrCreate(_methodAsync);
        _invokerComplex = _factory.GetOrCreate(_methodComplex);

        _args = ["test", 42];
        _complexArgs = [new ComplexInput { Name = "test", Values = [1, 2, 3] }];
    }

    #region 无参数方法测试

    [Benchmark(Baseline = true, Description = "Reflection - No Args")]
    public object? Reflection_NoArgs()
    {
        return _methodNoArgs.Invoke(_service, null);
    }

    [Benchmark(Description = "Expression Tree - No Args")]
    public object? Expression_NoArgs()
    {
        return _invokerNoArgs.Invoke(_service, null);
    }

    [Benchmark(Description = "Direct Call - No Args")]
    public string Direct_NoArgs()
    {
        return _service.NoArgsMethod();
    }

    #endregion

    #region 带参数方法测试

    [Benchmark(Baseline = true, Description = "Reflection - With Args")]
    public object? Reflection_WithArgs()
    {
        return _methodWithArgs.Invoke(_service, _args);
    }

    [Benchmark(Description = "Expression Tree - With Args")]
    public object? Expression_WithArgs()
    {
        return _invokerWithArgs.Invoke(_service, _args);
    }

    [Benchmark(Description = "Direct Call - With Args")]
    public string Direct_WithArgs()
    {
        return _service.WithArgsMethod("test", 42);
    }

    #endregion

    #region 异步方法测试

    [Benchmark(Baseline = true, Description = "Reflection - Async")]
    public async Task<object?> Reflection_Async()
    {
        var result = _methodAsync.Invoke(_service, ["value"]);
        return await (Task<string>)result!;
    }

    [Benchmark(Description = "Expression Tree - Async")]
    public async Task<object?> Expression_Async()
    {
        return await _invokerAsync.InvokeAsync(_service, ["value"]);
    }

    [Benchmark(Description = "Direct Call - Async")]
    public async Task<string> Direct_Async()
    {
        return await _service.AsyncMethod("value");
    }

    #endregion

    #region 复杂参数方法测试

    [Benchmark(Baseline = true, Description = "Reflection - Complex")]
    public object? Reflection_Complex()
    {
        return _methodComplex.Invoke(_service, _complexArgs);
    }

    [Benchmark(Description = "Expression Tree - Complex")]
    public object? Expression_Complex()
    {
        return _invokerComplex.Invoke(_service, _complexArgs);
    }

    [Benchmark(Description = "Direct Call - Complex")]
    public ComplexResult Direct_Complex()
    {
        return _service.ComplexMethod(new ComplexInput { Name = "test", Values = [1, 2, 3] });
    }

    #endregion
}

/// <summary>
/// 调用器缓存性能测试
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
public class InvokerCacheBenchmark
{
    private MethodInfo[] _methods = null!;
    private MethodInvokerFactory _factory = null!;

    [GlobalSetup]
    public void Setup()
    {
        var type = typeof(TestService);
        _methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<Common.Tools.McpToolAttribute>() != null)
            .ToArray();

        _factory = new MethodInvokerFactory();
    }

    [Benchmark(Description = "Create New Invoker (First Time)")]
    public void CreateNewInvoker()
    {
        var tempFactory = new MethodInvokerFactory();
        foreach (var method in _methods)
        {
            tempFactory.GetOrCreate(method);
        }
    }

    [Benchmark(Description = "Get Cached Invoker (Subsequent)")]
    public void GetCachedInvoker()
    {
        foreach (var method in _methods)
        {
            _factory.GetOrCreate(method);
        }
    }

    [Benchmark(Description = "Invoke with Cached Invoker")]
    public void InvokeWithCachedInvoker()
    {
        var service = new TestService();
        foreach (var method in _methods)
        {
            var invoker = _factory.GetOrCreate(method);
            var parameters = method.GetParameters();
            var args = parameters.Select(p => GetDefaultValue(p.ParameterType)).ToArray();
            invoker.Invoke(service, args);
        }
    }

    private static object? GetDefaultValue(Type type)
    {
        return type.Name switch
        {
            nameof(String) => "test",
            nameof(Int32) => 42,
            nameof(ComplexInput) => new ComplexInput { Name = "test", Values = [1, 2, 3] },
            _ => null
        };
    }
}

/// <summary>
/// 测试服务类
/// </summary>
public class TestService
{
    [Common.Tools.McpTool("test_no_args", "Test method with no arguments")]
    public string NoArgsMethod()
    {
        return "result";
    }

    [Common.Tools.McpTool("test_with_args", "Test method with arguments")]
    public string WithArgsMethod(string name, int count)
    {
        return $"{name}:{count}";
    }

    [Common.Tools.McpTool("test_async", "Test async method")]
    public async Task<string> AsyncMethod(string value)
    {
        await Task.Delay(1);
        return $"async:{value}";
    }

    [Common.Tools.McpTool("test_complex", "Test complex method")]
    public ComplexResult ComplexMethod(ComplexInput input)
    {
        return new ComplexResult
        {
            Name = input.Name.ToUpperInvariant(),
            Count = input.Values.Sum()
        };
    }
}

public class ComplexInput
{
    public string Name { get; set; } = string.Empty;
    public int[] Values { get; set; } = [];
}

public class ComplexResult
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}
