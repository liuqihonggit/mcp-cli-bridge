namespace Service.Json.Contracts;

/// <summary>
/// JSON-RPC 消息基类
/// </summary>
public abstract class JsonRpcMessage
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";
}

/// <summary>
/// JSON-RPC 请求
/// </summary>
public class JsonRpcRequest : JsonRpcMessage
{
    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public object? Params { get; set; }
}

/// <summary>
/// JSON-RPC 响应
/// </summary>
public class JsonRpcResponse : JsonRpcMessage
{
    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; set; }
}

/// <summary>
/// JSON-RPC 错误
/// </summary>
public class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

/// <summary>
/// JSON-RPC 通知
/// </summary>
public class JsonRpcNotification : JsonRpcMessage
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public object? Params { get; set; }
}

/// <summary>
/// 初始化请求参数
/// </summary>
public class InitializeRequestParams
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "2024-11-05";

    [JsonPropertyName("capabilities")]
    public ClientCapabilities Capabilities { get; set; } = new();

    [JsonPropertyName("clientInfo")]
    public Implementation ClientInfo { get; set; } = new();
}

/// <summary>
/// 初始化结果
/// </summary>
public class InitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "2024-11-05";

    [JsonPropertyName("capabilities")]
    public ServerCapabilities Capabilities { get; set; } = new();

    [JsonPropertyName("serverInfo")]
    public Implementation ServerInfo { get; set; } = new();
}

/// <summary>
/// 客户端能力
/// </summary>
public class ClientCapabilities
{
    [JsonPropertyName("sampling")]
    public object? Sampling { get; set; }

    [JsonPropertyName("roots")]
    public object? Roots { get; set; }
}

/// <summary>
/// 服务端能力
/// </summary>
public class ServerCapabilities
{
    [JsonPropertyName("tools")]
    public ToolsCapability? Tools { get; set; }
}

/// <summary>
/// 工具能力
/// </summary>
public class ToolsCapability
{
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; }
}

/// <summary>
/// 实现信息
/// </summary>
public class Implementation
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// 列出工具结果
/// </summary>
public class ListToolsResult
{
    [JsonPropertyName("tools")]
    public List<ToolDefinition> Tools { get; set; } = [];
}

/// <summary>
/// 输入 Schema
/// </summary>
public class InputSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, PropertySchema> Properties { get; set; } = [];

    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = [];
}

/// <summary>
/// 属性 Schema
/// </summary>
public class PropertySchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// 调用工具请求参数
/// </summary>
public class CallToolRequestParams
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public object? Arguments { get; set; }
}

/// <summary>
/// 调用工具结果
/// </summary>
public class CallToolResult
{
    [JsonPropertyName("content")]
    public List<ToolContent> Content { get; set; } = [];

    [JsonPropertyName("isError")]
    public bool IsError { get; set; }
}

/// <summary>
/// 工具内容
/// </summary>
public class ToolContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
