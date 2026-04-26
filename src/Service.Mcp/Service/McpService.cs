global using static Common.Constants.ConstantManager;
global using static Common.Constants.ConstantManager.Commands;

using Common.Plugins;
using Mcp = Common.Constants.ConstantManager.Commands.Mcp;

namespace Service.Mcp.Server;

public class McpServer
{
    private readonly Dictionary<string, CompiledToolHandler> _tools = new();
    private readonly Dictionary<string, object> _toolInstances = new();
    private readonly ToolMethodRegistry _methodRegistry;
    private readonly McpToolMethodInvoker _methodInvoker;
    private readonly string _serverName;
    private readonly string _serverVersion;

    public McpServer(string serverName = "McpServer", string? serverVersion = null)
    {
        _serverName = serverName;
        _serverVersion = serverVersion ?? Versions.McpHost;
        _methodRegistry = new ToolMethodRegistry(new MethodInvokerFactory());
        _methodInvoker = new McpToolMethodInvoker();
    }

    public void RegisterTool<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T>(T toolInstance) where T : class
    {
        ArgumentNullException.ThrowIfNull(toolInstance);
        RegisterTool(new ToolRegistrationOptions
        {
            ToolInstance = toolInstance
        });
    }

    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "工具类型在编译时已知")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "工具类型由TrimmerRootAssembly保留")]
    [UnconditionalSuppressMessage("Trimming", "IL2075:DynamicallyAccessedMembers", Justification = "工具类型由TrimmerRootAssembly保留")]
    public void RegisterTool(ToolRegistrationOptions options)
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

            foreach (var param in parameters)
            {
                var descAttr = param.GetCustomAttribute<McpParameterAttribute>();
                if (descAttr != null && !string.IsNullOrEmpty(param.Name))
                {
                    paramDescriptions[param.Name] = descAttr.Description;
                }
            }

            // 检查是否允许覆盖
            if (!options.AllowOverride && _tools.ContainsKey(toolName))
            {
                continue;
            }

            // 使用编译后的调用器
            var invoker = _methodRegistry.TryGetToolMethod(toolName, out var existingMetadata)
                ? existingMetadata.Invoker
                : new MethodInvokerFactory().GetOrCreate(method);

            // 获取增强描述
            var enhancedDescription = attr.GetEnhancedDescription();
            var fullDescription = enhancedDescription != null
                ? $"{description}\n\n{enhancedDescription.GenerateFullDescription()}"
                : description;

            var handler = new CompiledToolHandler
            {
                Name = toolName,
                Description = description,
                FullDescription = fullDescription,
                Category = attr.Category,
                DefaultTimeout = attr.DefaultTimeout,
                RequiredPermissions = attr.GetRequiredPermissions(),
                Method = method,
                Instance = toolInstance,
                Invoker = invoker,
                Parameters = parameters,
                ParameterDescriptions = paramDescriptions
            };

            _tools[toolName] = handler;
            _toolInstances[toolName] = toolInstance;
        }
    }

    public async Task RunAsync()
    {
        var stdin = Console.OpenStandardInput();
        var stdout = Console.OpenStandardOutput();
        var reader = new StreamReader(stdin);
        var writer = new StreamWriter(stdout) { AutoFlush = true };

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line == null)
            {
                break;
            }
            if (string.IsNullOrEmpty(line))
            {
                await Task.Delay((int)Timeouts.ProcessExitDelay.TotalMilliseconds);
                continue;
            }

            var trimmedLine = line.TrimStart();

            if (trimmedLine.StartsWith("{"))
            {
                var response = await ProcessMessageAsync(trimmedLine);
                if (response != null)
                {
                    var responseJson = McpJsonSerializer.Serialize(response);
                    await writer.WriteLineAsync(responseJson);
                    await writer.FlushAsync();
                }
                continue;
            }

            if (!line.StartsWith(JsonRpc.ContentLengthPrefix)) continue;

            var contentLength = int.Parse(line.Substring(JsonRpc.ContentLengthPrefix.Length).Trim());
            await reader.ReadLineAsync();

            var buffer = new char[contentLength];
            var read = await reader.ReadAsync(buffer, 0, contentLength);
            var json = new string(buffer, 0, read);

            var responseLsp = await ProcessMessageAsync(json);
            if (responseLsp != null)
            {
                var responseJson = McpJsonSerializer.Serialize(responseLsp);
                var responseBytes = System.Text.Encoding.UTF8.GetBytes(responseJson);
                await writer.WriteLineAsync($"Content-Length: {responseBytes.Length}");
                await writer.WriteLineAsync();
                await writer.WriteAsync(responseJson);
                await writer.FlushAsync();
            }
        }
    }

    private async Task<JsonRpcResponse?> ProcessMessageAsync(string json)
    {
        JsonRpcRequest? request = null;
        try
        {
            request = McpJsonSerializer.Deserialize<JsonRpcRequest>(json);
            if (request == null) return null;

            var requestId = ExtractRequestId(request.Id);

            if (requestId == null)
            {
                if (request.Method == "initialized")
                {
                }
                return null;
            }

            var response = new JsonRpcResponse { Id = requestId };

            switch (request.Method)
            {
                case "initialize":
                    response.Result = HandleInitialize();
                    break;

                case "tools/list":
                    response.Result = HandleListTools();
                    break;

                case "tools/call":
                    response.Result = await HandleCallToolAsync(request.Params);
                    break;

                default:
                    response.Error = new JsonRpcError
                    {
                        Code = ErrorCodes.MethodNotFound,
                        Message = $"Method not found: {request.Method}"
                    };
                    break;
            }

            return response;
        }
        catch (Exception ex)
        {
            return new JsonRpcResponse
            {
                Id = request?.Id ?? null,
                Error = new JsonRpcError
                {
                    Code = ErrorCodes.InternalError,
                    Message = $"Internal error: {ex.Message}"
                }
            };
        }
    }

    private static object? ExtractRequestId(object? id)
    {
        if (id == null) return null;

        if (id is System.Text.Json.JsonElement element)
        {
            return element.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Number => TryGetInt64(element) ?? element.GetInt64(),
                System.Text.Json.JsonValueKind.String => element.GetString(),
                _ => null
            };
        }

        return id;
    }

    private static object? TryGetInt64(System.Text.Json.JsonElement element)
    {
        if (element.TryGetInt32(out var intValue))
        {
            return intValue;
        }

        return element.GetInt64();
    }

    private InitializeResult HandleInitialize()
    {
        return new InitializeResult
        {
            ProtocolVersion = JsonRpc.ProtocolVersion,
            Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability { ListChanged = false }
            },
            ServerInfo = new Implementation
            {
                Name = _serverName,
                Version = _serverVersion
            }
        };
    }

    private ListToolsResult HandleListTools()
    {
        var tools = _tools.Values.Select(h => new ToolDefinition
        {
            Name = h.Name,
            Description = h.FullDescription,
            InputSchema = GenerateInputSchema(h)
        }).ToList();

        return new ListToolsResult { Tools = tools };
    }

    private async Task<CallToolResult> HandleCallToolAsync(object? paramsObj)
    {
        if (paramsObj == null)
            return new CallToolResult { Content = [new ToolContent { Text = "No parameters provided" }], IsError = true };

        var callParams = paramsObj is JsonElement element
            ? McpJsonSerializer.Deserialize<CallToolRequestParams>(element.GetRawText())
            : McpJsonSerializer.Deserialize<CallToolRequestParams>(McpJsonSerializer.Serialize(paramsObj));
        if (callParams == null)
            return new CallToolResult { Content = [new ToolContent { Text = "Invalid parameters" }], IsError = true };

        if (!_tools.TryGetValue(callParams.Name, out var handler))
            return new CallToolResult { Content = [new ToolContent { Text = $"Tool not found: {callParams.Name}" }], IsError = true };

        try
        {
            // 使用编译后的调用器执行方法
            var args = ParseArguments(handler, callParams.Arguments);
            var result = await handler.Invoker.InvokeAsync(handler.Instance, args).ConfigureAwait(false);

            var resultText = result switch
            {
                null => "null",
                string s => s,
                _ => McpJsonSerializer.SerializeObject(result)
            };
            return new CallToolResult
            {
                Content = [new ToolContent { Text = resultText }]
            };
        }
        catch (Exception ex)
        {
            return new CallToolResult
            {
                Content = [new ToolContent { Text = $"Error: {ex.InnerException?.Message ?? ex.Message}" }],
                IsError = true
            };
        }
    }

    private object?[] ParseArguments(CompiledToolHandler handler, object? arguments)
    {
        if (handler.Parameters.Length == 0) return [];

        var argsJson = arguments is JsonElement element
            ? element.GetRawText()
            : "{}";

        var argsDict = McpJsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argsJson) ?? new();

        var result = new object?[handler.Parameters.Length];
        for (int i = 0; i < handler.Parameters.Length; i++)
        {
            var param = handler.Parameters[i];
            if (argsDict.TryGetValue(param.Name!, out var value))
            {
                result[i] = DeserializeArgument(value, param.ParameterType);
            }
            else
            {
                result[i] = param.DefaultValue;
            }
        }

        return result;
    }

    private static object? DeserializeArgument(JsonElement value, Type targetType)
    {
        if (targetType == typeof(string))
            return value.GetString();
        if (targetType == typeof(int) || targetType == typeof(int?))
            return value.GetInt32();
        if (targetType == typeof(long) || targetType == typeof(long?))
            return value.GetInt64();
        if (targetType == typeof(double) || targetType == typeof(double?))
            return value.GetDouble();
        if (targetType == typeof(float) || targetType == typeof(float?))
            return value.GetSingle();
        if (targetType == typeof(bool) || targetType == typeof(bool?))
            return value.GetBoolean();
        if (targetType == typeof(Dictionary<string, JsonElement>))
            return McpJsonSerializer.Deserialize<Dictionary<string, JsonElement>>(value.GetRawText());
        if (targetType == typeof(JsonElement))
            return value;
        if (targetType == typeof(object))
            return value;

        // AOT兼容：对于未知类型，返回JsonElement而不是尝试反序列化
        return value;
    }

    private static JsonElement GenerateInputSchema(CompiledToolHandler handler)
    {
        var schema = new InputSchema
        {
            Type = JsonValueTypes.Object,
            Properties = [],
            Required = []
        };

        foreach (var param in handler.Parameters)
        {
            var paramSchema = new PropertySchema
            {
                Type = GetJsonType(param.ParameterType)
            };

            if (handler.ParameterDescriptions.TryGetValue(param.Name!, out var description))
            {
                paramSchema.Description = description;
            }

            schema.Properties[param.Name!] = paramSchema;

            if (!param.IsOptional)
            {
                schema.Required.Add(param.Name!);
            }
        }

        var json = JsonSerializer.Serialize(schema, CommonJsonContext.Default.InputSchema);
        return JsonSerializer.Deserialize<JsonElement>(json, CommonJsonContext.Default.JsonElement);
    }

    private static readonly Dictionary<Type, string> JsonTypeCache = new()
    {
        [typeof(string)] = JsonValueTypes.String,
        [typeof(int)] = JsonValueTypes.Integer,
        [typeof(long)] = JsonValueTypes.Integer,
        [typeof(double)] = JsonValueTypes.Number,
        [typeof(float)] = JsonValueTypes.Number,
        [typeof(bool)] = JsonValueTypes.Boolean
    };

    private static string GetJsonType(Type type)
    {
        if (JsonTypeCache.TryGetValue(type, out var jsonType))
            return jsonType;

        if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
            return JsonValueTypes.Array;

        return JsonValueTypes.Object;
    }

    private sealed class CompiledToolHandler
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FullDescription { get; set; } = string.Empty;
        public string Category { get; set; } = "general";
        public int DefaultTimeout { get; set; } = 30000;
        public IReadOnlyList<string> RequiredPermissions { get; set; } = [];
        public MethodInfo Method { get; set; } = null!;
        public object Instance { get; set; } = null!;
        public IMethodInvoker Invoker { get; set; } = null!;
        public ParameterInfo[] Parameters { get; set; } = [];
        public Dictionary<string, string> ParameterDescriptions { get; set; } = new(StringComparer.Ordinal);
    }
}
