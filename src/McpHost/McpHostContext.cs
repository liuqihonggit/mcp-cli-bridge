namespace McpHost;

[JsonSerializable(typeof(PluginManager.PluginConfiguration))]
[JsonSerializable(typeof(PluginManager.CliProviderConfiguration))]
[JsonSerializable(typeof(PluginManager.CliToolConfiguration))]
[JsonSerializable(typeof(List<PluginManager.CliProviderConfiguration>))]
[JsonSerializable(typeof(List<PluginManager.CliToolConfiguration>))]
[JsonSerializable(typeof(Common.Contracts.ParameterEntry))]
[JsonSerializable(typeof(List<Common.Contracts.ParameterEntry>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
public partial class McpHostContext : JsonSerializerContext
{
}
