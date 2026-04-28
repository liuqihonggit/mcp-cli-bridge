namespace Service.Json;

// 基础集合类型
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, HashSet<string>>))]
[JsonSerializable(typeof(Dictionary<string, List<string>>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(HashSet<string>))]
[JsonSerializable(typeof(List<string>))]

// 新的统一操作结果模型
[JsonSerializable(typeof(OperationResult))]
[JsonSerializable(typeof(OperationResult<string>))]
[JsonSerializable(typeof(OperationResult<int>))]
[JsonSerializable(typeof(OperationResult<bool>))]
[JsonSerializable(typeof(OperationResult<List<string>>))]
[JsonSerializable(typeof(OperationResult<Dictionary<string, object>>))]
[JsonSerializable(typeof(OperationResult<List<ToolDefinition>>))]
[JsonSerializable(typeof(OperationResult<PluginDescriptor>))]
[JsonSerializable(typeof(OperationResult<KnowledgeGraphData>))]

// fix:问题是 OperationResult<object> 中的 Data 属性是 object 类型，在 AOT 编译后，JSON Source Generator 无法确定如何序列化这个 object 类型的数据。
[JsonSerializable(typeof(OperationResult<JsonElement>))]

// 安全模型
[JsonSerializable(typeof(SecurityAuditEntry))]
[JsonSerializable(typeof(ValidationError))]
[JsonSerializable(typeof(WhitelistConfiguration))]
[JsonSerializable(typeof(RbacConfiguration))]
[JsonSerializable(typeof(WhitelistConfigurationJsonModel))]
[JsonSerializable(typeof(RbacConfigurationJsonModel))]

// 知识图谱模型
[JsonSerializable(typeof(KnowledgeGraphEntity))]
[JsonSerializable(typeof(KnowledgeGraphRelation))]
[JsonSerializable(typeof(KnowledgeGraphData))]
[JsonSerializable(typeof(List<KnowledgeGraphEntity>))]
[JsonSerializable(typeof(List<KnowledgeGraphRelation>))]

// 工具定义模型
[JsonSerializable(typeof(ToolDefinition))]
[JsonSerializable(typeof(List<ToolDefinition>))]

// CLI 桥接模型
[JsonSerializable(typeof(ToolSearchResult))]
[JsonSerializable(typeof(List<ToolSearchResult>))]
[JsonSerializable(typeof(PluginDescriptor))]
[JsonSerializable(typeof(PluginDescriptor[]))]
[JsonSerializable(typeof(List<PluginDescriptor>))]
[JsonSerializable(typeof(CommandDescriptor))]
[JsonSerializable(typeof(List<CommandDescriptor>))]
[JsonSerializable(typeof(PluginDescribeResult))]
[JsonSerializable(typeof(ToolListResult))]
[JsonSerializable(typeof(ProviderListResult))]
[JsonSerializable(typeof(ProviderInfo))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(PackageStatusResult))]
[JsonSerializable(typeof(PackageInstallResult))]
[JsonSerializable(typeof(CountResult))]
[JsonSerializable(typeof(DeleteResult))]

// JSON Schema 模型
[JsonSerializable(typeof(JsonSchema))]
[JsonSerializable(typeof(JsonSchemaProperty))]
[JsonSerializable(typeof(Dictionary<string, JsonSchemaProperty>))]

// CLI 协议模型
[JsonSerializable(typeof(CliRequest))]

// 文件读取模型
[JsonSerializable(typeof(FileReaderRequest))]
[JsonSerializable(typeof(FileReadResult))]

// 示例模型
[JsonSerializable(typeof(CreateEntitiesExampleRequest))]
[JsonSerializable(typeof(ExampleEntity))]
[JsonSerializable(typeof(CreateRelationsExampleRequest))]
[JsonSerializable(typeof(ExampleRelation))]
[JsonSerializable(typeof(ReadGraphExampleRequest))]
[JsonSerializable(typeof(SearchNodesExampleRequest))]
[JsonSerializable(typeof(AddObservationsExampleRequest))]
[JsonSerializable(typeof(DeleteEntitiesExampleRequest))]
[JsonSerializable(typeof(OpenNodesExampleRequest))]

// 参数条目（用于日志序列化）
[JsonSerializable(typeof(ParameterEntry))]
[JsonSerializable(typeof(List<ParameterEntry>))]

// ID types for JSON-RPC
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(string))]

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class CommonJsonContext : JsonSerializerContext
{
}
