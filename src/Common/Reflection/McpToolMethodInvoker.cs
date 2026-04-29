namespace Common.Reflection;

/// <summary>
/// MCP工具方法调用器，结合参数解析和表达式树编译
/// 支持从JsonElement参数直接调用工具方法
/// </summary>
public sealed class McpToolMethodInvoker
{
    private readonly IMethodInvokerFactory _invokerFactory;
    private readonly ConcurrentDictionary<MethodInfo, ParameterBinder> _parameterBinders = new();

    /// <summary>
    /// 使用默认调用器工厂创建MCP工具方法调用器
    /// </summary>
    public McpToolMethodInvoker() : this(new MethodInvokerFactory())
    {
    }

    /// <summary>
    /// 使用指定的调用器工厂创建MCP工具方法调用器
    /// </summary>
    /// <param name="invokerFactory">方法调用器工厂</param>
    public McpToolMethodInvoker(IMethodInvokerFactory invokerFactory)
    {
        _invokerFactory = invokerFactory ?? throw new ArgumentNullException(nameof(invokerFactory));
    }

    /// <summary>
    /// 调用工具方法
    /// </summary>
    /// <param name="instance">工具实例</param>
    /// <param name="method">方法信息</param>
    /// <param name="arguments">JSON参数字典</param>
    /// <returns>调用结果</returns>
    public async Task<object?> InvokeAsync(
        object instance,
        MethodInfo method,
        IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(method);

        var invoker = _invokerFactory.GetOrCreate(method);
        var binder = GetOrCreateParameterBinder(method);
        var args = binder.BindArguments(arguments);

        return await invoker.InvokeAsync(instance, args).ConfigureAwait(false);
    }

    /// <summary>
    /// 同步调用工具方法
    /// </summary>
    /// <param name="instance">工具实例</param>
    /// <param name="method">方法信息</param>
    /// <param name="arguments">JSON参数字典</param>
    /// <returns>调用结果</returns>
    public object? Invoke(
        object instance,
        MethodInfo method,
        IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(method);

        var invoker = _invokerFactory.GetOrCreate(method);
        var binder = GetOrCreateParameterBinder(method);
        var args = binder.BindArguments(arguments);

        return invoker.Invoke(instance, args);
    }

    /// <summary>
    /// 获取或创建参数绑定器
    /// </summary>
    private ParameterBinder GetOrCreateParameterBinder(MethodInfo method)
    {
        return _parameterBinders.GetOrAdd(method, static m => new ParameterBinder(m));
    }

    /// <summary>
    /// 获取缓存的调用器数量
    /// </summary>
    public int CachedInvokerCount => _invokerFactory.CachedCount;

    /// <summary>
    /// 获取缓存的参数绑定器数量
    /// </summary>
    public int CachedBinderCount => _parameterBinders.Count;

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    public void ClearCache()
    {
        _invokerFactory.Clear();
        _parameterBinders.Clear();
    }
}

/// <summary>
/// 参数绑定器，将JSON参数绑定到方法参数
/// </summary>
internal sealed class ParameterBinder
{
    private readonly ParameterInfo[] _parameters;
    private readonly Dictionary<string, int> _parameterIndexMap;
    private readonly Dictionary<int, Func<JsonElement, object?>> _deserializers;

    public ParameterBinder(MethodInfo method)
    {
        _parameters = method.GetParameters();
        _parameterIndexMap = new Dictionary<string, int>(_parameters.Length, StringComparer.Ordinal);
        _deserializers = new Dictionary<int, Func<JsonElement, object?>>();

        for (int i = 0; i < _parameters.Length; i++)
        {
            var param = _parameters[i];
            _parameterIndexMap[param.Name!] = i;
            _deserializers[i] = CreateDeserializer(param.ParameterType);
        }
    }

    /// <summary>
    /// 绑定参数
    /// </summary>
    public object?[] BindArguments(IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        if (_parameters.Length == 0)
        {
            return Array.Empty<object?>();
        }

        var result = new object?[_parameters.Length];

        for (int i = 0; i < _parameters.Length; i++)
        {
            var param = _parameters[i];
            var paramName = param.Name!;

            if (arguments != null && arguments.TryGetValue(paramName, out var jsonValue))
            {
                result[i] = _deserializers[i](jsonValue);
            }
            else if (param.HasDefaultValue)
            {
                result[i] = param.DefaultValue;
            }
            else
            {
                result[i] = GetDefaultValue(param.ParameterType);
            }
        }

        return result;
    }

    /// <summary>
    /// 获取类型的默认值
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL2067", Justification = "ValueType创建在AOT下安全")]
    private static object? GetDefaultValue(Type type)
    {
        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }
        return null;
    }

    /// <summary>
    /// 创建类型反序列化器
    /// </summary>
    private static Func<JsonElement, object?> CreateDeserializer(Type targetType)
    {
        // 基本类型优化：直接返回JsonElement方法
        if (targetType == typeof(string))
        {
            return element => element.GetString();
        }
        if (targetType == typeof(int) || targetType == typeof(int?))
        {
            return element => element.GetInt32();
        }
        if (targetType == typeof(long) || targetType == typeof(long?))
        {
            return element => element.GetInt64();
        }
        if (targetType == typeof(double) || targetType == typeof(double?))
        {
            return element => element.GetDouble();
        }
        if (targetType == typeof(float) || targetType == typeof(float?))
        {
            return element => element.GetSingle();
        }
        if (targetType == typeof(bool) || targetType == typeof(bool?))
        {
            return element => element.GetBoolean();
        }
        if (targetType == typeof(decimal) || targetType == typeof(decimal?))
        {
            return element => element.GetDecimal();
        }
        if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
        {
            return element => element.GetDateTime();
        }
        if (targetType == typeof(DateTimeOffset) || targetType == typeof(DateTimeOffset?))
        {
            return element => element.GetDateTimeOffset();
        }
        if (targetType == typeof(Guid) || targetType == typeof(Guid?))
        {
            return element => element.GetGuid();
        }
        if (targetType == typeof(byte[]) || targetType == typeof(byte?[]))
        {
            return element => element.GetBytesFromBase64();
        }
        if (targetType == typeof(JsonElement))
        {
            return element => element;
        }
        if (targetType == typeof(Dictionary<string, JsonElement>))
        {
            return element => JsonSerializer.Deserialize(element.GetRawText(), CommonJsonContext.Default.DictionaryStringJsonElement);
        }

        // 复杂类型：使用JsonNode作为中间格式避免反射
        return element => DeserializeComplexType(element, targetType);
    }

    /// <summary>
    /// 反序列化复杂类型 - 使用JsonNode避免直接反射
    /// </summary>
    private static object? DeserializeComplexType(JsonElement element, Type targetType)
    {
        // 对于复杂类型，返回JsonElement本身，由调用方处理
        // 这样可以避免在AOT环境下使用反射进行反序列化
        if (targetType == typeof(object))
        {
            return element;
        }

        // 尝试使用JsonNode转换
        try
        {
            var jsonNode = JsonNode.Parse(element.GetRawText());
            return ConvertJsonNodeToType(jsonNode, targetType);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 将JsonNode转换为指定类型
    /// </summary>
    private static object? ConvertJsonNodeToType(JsonNode? node, Type targetType)
    {
        if (node is null)
        {
            return null;
        }

        // 处理基本类型
        if (targetType == typeof(string))
        {
            return node.GetValue<string>();
        }
        if (targetType == typeof(int) || targetType == typeof(int?))
        {
            return node.GetValue<int>();
        }
        if (targetType == typeof(long) || targetType == typeof(long?))
        {
            return node.GetValue<long>();
        }
        if (targetType == typeof(double) || targetType == typeof(double?))
        {
            return node.GetValue<double>();
        }
        if (targetType == typeof(bool) || targetType == typeof(bool?))
        {
            return node.GetValue<bool>();
        }

        // 对于其他复杂类型，返回JsonElement
        if (node is JsonObject || node is JsonArray)
        {
            var jsonString = node.ToJsonString();
            using var doc = JsonDocument.Parse(jsonString);
            return doc.RootElement.Clone();
        }

        return node.GetValue<object>();
    }
}

/// <summary>
/// 工具方法元数据
/// </summary>
public sealed class ToolMethodMetadata
{
    /// <summary>
    /// 工具名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 工具描述
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 方法信息
    /// </summary>
    public required MethodInfo Method { get; init; }

    /// <summary>
    /// 方法调用器
    /// </summary>
    public IMethodInvoker Invoker { get; internal set; } = null!;

    /// <summary>
    /// 参数信息
    /// </summary>
    public IReadOnlyList<ParameterMetadata> Parameters { get; internal set; } = [];

    /// <summary>
    /// 参数描述字典
    /// </summary>
    public IReadOnlyDictionary<string, string> ParameterDescriptions { get; internal set; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// 参数元数据
/// </summary>
public sealed class ParameterMetadata
{
    /// <summary>
    /// 参数名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 参数类型
    /// </summary>
    public required Type Type { get; init; }

    /// <summary>
    /// 是否可选
    /// </summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// 默认值
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// 工具方法注册器，用于扫描和注册工具方法
/// </summary>
public sealed class ToolMethodRegistry
{
    private readonly IMethodInvokerFactory _invokerFactory;
    private readonly ConcurrentDictionary<string, ToolMethodMetadata> _toolMethods = new();

    public ToolMethodRegistry(IMethodInvokerFactory invokerFactory)
    {
        _invokerFactory = invokerFactory ?? throw new ArgumentNullException(nameof(invokerFactory));
    }

    /// <summary>
    /// 注册工具实例的所有工具方法
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL2090", Justification = "工具类型在编译时已知")]
    public void RegisterToolInstance<T>(T toolInstance) where T : class
    {
        ArgumentNullException.ThrowIfNull(toolInstance);
        RegisterToolInstance(new ToolRegistrationOptions
        {
            ToolInstance = toolInstance
        });
    }

    /// <summary>
    /// 使用注册选项注册工具实例的所有工具方法
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "工具类型在编译时已知")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "工具类型由TrimmerRootAssembly保留")]
    [UnconditionalSuppressMessage("Trimming", "IL2075:DynamicallyAccessedMembers", Justification = "工具类型由TrimmerRootAssembly保留")]
    public void RegisterToolInstance(ToolRegistrationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.ToolInstance);

        var toolInstance = options.ToolInstance;
        var type = toolInstance.GetType();
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<McpToolAttribute>();
            if (attr == null) continue;

            var toolName = options.ToolName ?? attr.Name ?? method.Name;
            var description = options.Description ?? attr.Description;
            var parameters = method.GetParameters();

            var paramDescriptions = new Dictionary<string, string>(StringComparer.Ordinal);
            var paramMetadataList = new List<ParameterMetadata>(parameters.Length);

            foreach (var param in parameters)
            {
                var descAttr = param.GetCustomAttribute<McpParameterAttribute>();
                var paramDescription = descAttr?.Description ?? string.Empty;

                if (!string.IsNullOrEmpty(param.Name))
                {
                    paramDescriptions[param.Name] = paramDescription;
                }

                paramMetadataList.Add(new ParameterMetadata
                {
                    Name = param.Name ?? $"arg{paramMetadataList.Count}",
                    Type = param.ParameterType,
                    IsOptional = param.IsOptional,
                    DefaultValue = param.HasDefaultValue ? param.DefaultValue : null,
                    Description = paramDescription
                });
            }

            var invoker = _invokerFactory.GetOrCreate(method);

            var metadata = new ToolMethodMetadata
            {
                Name = toolName,
                Description = description,
                Method = method,
                Invoker = invoker,
                Parameters = paramMetadataList.AsReadOnly(),
                ParameterDescriptions = paramDescriptions
            };

            if (!options.AllowOverride && _toolMethods.ContainsKey(toolName))
            {
                continue;
            }

            _toolMethods[toolName] = metadata;
        }
    }

    /// <summary>
    /// 尝试获取工具方法元数据
    /// </summary>
    public bool TryGetToolMethod(string toolName, [NotNullWhen(true)] out ToolMethodMetadata? metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return _toolMethods.TryGetValue(toolName, out metadata);
    }

    /// <summary>
    /// 获取所有工具方法元数据
    /// </summary>
    public IReadOnlyList<ToolMethodMetadata> GetAllToolMethods()
    {
        return _toolMethods.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// 获取注册的工具数量
    /// </summary>
    public int ToolCount => _toolMethods.Count;

    /// <summary>
    /// 清除所有注册
    /// </summary>
    public void Clear()
    {
        _toolMethods.Clear();
    }
}
