namespace McpProtocol.Contracts;

[JsonSerializable(typeof(JsonRpcRequest))]
[JsonSerializable(typeof(JsonRpcResponse))]
[JsonSerializable(typeof(JsonRpcError))]
[JsonSerializable(typeof(JsonRpcNotification))]
[JsonSerializable(typeof(InitializeRequestParams))]
[JsonSerializable(typeof(InitializeResult))]
[JsonSerializable(typeof(ClientCapabilities))]
[JsonSerializable(typeof(ServerCapabilities))]
[JsonSerializable(typeof(ToolsCapability))]
[JsonSerializable(typeof(Implementation))]
[JsonSerializable(typeof(ListToolsResult))]
[JsonSerializable(typeof(InputSchema))]
[JsonSerializable(typeof(PropertySchema))]
[JsonSerializable(typeof(Dictionary<string, PropertySchema>))]
[JsonSerializable(typeof(CallToolRequestParams))]
[JsonSerializable(typeof(CallToolResult))]
[JsonSerializable(typeof(ToolContent))]
[JsonSerializable(typeof(List<ToolContent>))]
[JsonSerializable(typeof(ToolDefinition))]
[JsonSerializable(typeof(List<ToolDefinition>))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(string))]

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class McpJsonContext : JsonSerializerContext
{
}
