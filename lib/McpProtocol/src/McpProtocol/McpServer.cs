namespace McpProtocol;

public class McpServer : IMcpServer
{
    private readonly Dictionary<string, IToolHandler> _tools = new(StringComparer.Ordinal);
    private readonly string _serverName;
    private readonly string _serverVersion;

    public McpServer(string serverName = "McpServer", string? serverVersion = null)
    {
        _serverName = serverName;
        _serverVersion = serverVersion ?? "1.0.0";
    }

    public void RegisterTool<T>(T toolInstance) where T : class
    {
        ArgumentNullException.ThrowIfNull(toolInstance);

        if (toolInstance is IToolHandler handler)
        {
            _tools[handler.Name] = handler;
        }
        else
        {
            throw new ArgumentException($"Tool instance must implement {nameof(IToolHandler)}", nameof(toolInstance));
        }
    }

    public void RegisterToolHandler(IToolHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _tools[handler.Name] = handler;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var stdin = Console.OpenStandardInput();
        var stdout = Console.OpenStandardOutput();
        var reader = new StreamReader(stdin);
        var writer = new StreamWriter(stdout) { AutoFlush = true };

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
            {
                break;
            }
            if (string.IsNullOrEmpty(line))
            {
                await Task.Delay(100, cancellationToken);
                continue;
            }

            var trimmedLine = line.TrimStart();

            if (trimmedLine.StartsWith("{"))
            {
                var response = await ProcessMessageAsync(trimmedLine, cancellationToken);
                if (response != null)
                {
                    var responseJson = McpJsonSerializer.Serialize(response);
                    await writer.WriteLineAsync(responseJson);
                    await writer.FlushAsync(cancellationToken);
                }
                continue;
            }

            if (!line.StartsWith(JsonRpc.ContentLengthPrefix)) continue;

            var contentLength = int.Parse(line.Substring(JsonRpc.ContentLengthPrefix.Length).Trim());
            await reader.ReadLineAsync(cancellationToken);

            var buffer = new char[contentLength];
            var read = await reader.ReadAsync(buffer, 0, contentLength);
            var json = new string(buffer, 0, read);

            var responseLsp = await ProcessMessageAsync(json, cancellationToken);
            if (responseLsp != null)
            {
                var responseJson = McpJsonSerializer.Serialize(responseLsp);
                var responseBytes = System.Text.Encoding.UTF8.GetBytes(responseJson);
                await writer.WriteLineAsync($"Content-Length: {responseBytes.Length}");
                await writer.WriteLineAsync();
                await writer.WriteAsync(responseJson);
                await writer.FlushAsync(cancellationToken);
            }
        }
    }

    private async Task<JsonRpcResponse?> ProcessMessageAsync(string json, CancellationToken cancellationToken)
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
                    response.Result = await HandleCallToolAsync(request.Params, cancellationToken);
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

        if (id is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number => TryGetInt64(element) ?? element.GetInt64(),
                JsonValueKind.String => element.GetString(),
                _ => null
            };
        }

        return id;
    }

    private static object? TryGetInt64(JsonElement element)
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
            ProtocolVersion = McpProtocolVersion.Current,
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
            Description = h.Description,
            InputSchema = CreateEmptyInputSchema()
        }).ToList();

        return new ListToolsResult { Tools = tools };
    }

    private static JsonElement CreateEmptyInputSchema()
    {
        var json = @"{""type"":""object"",""properties"":{},""required"":[]}";
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private async Task<CallToolResult> HandleCallToolAsync(object? paramsObj, CancellationToken cancellationToken)
    {
        if (paramsObj == null)
            return new CallToolResult { Content = [new ToolContent { Text = "No parameters provided" }], IsError = true };

        var callParams = paramsObj is JsonElement element
            ? McpJsonSerializer.Deserialize<CallToolRequestParams>(element.GetRawText())
            : McpJsonSerializer.Deserialize<CallToolRequestParams>(McpJsonSerializer.SerializeObject(paramsObj));
        if (callParams == null)
            return new CallToolResult { Content = [new ToolContent { Text = "Invalid parameters" }], IsError = true };

        if (!_tools.TryGetValue(callParams.Name, out var handler))
            return new CallToolResult { Content = [new ToolContent { Text = $"Tool not found: {callParams.Name}" }], IsError = true };

        try
        {
            var arguments = ParseArguments(callParams.Arguments);
            var result = await handler.ExecuteAsync(arguments);

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

    private static Dictionary<string, JsonElement> ParseArguments(object? arguments)
    {
        if (arguments == null) return new Dictionary<string, JsonElement>();

        var argsJson = arguments is JsonElement element
            ? element.GetRawText()
            : "{}";

        return McpJsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argsJson) ?? new Dictionary<string, JsonElement>();
    }
}
