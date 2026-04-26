namespace McpHost;

[JsonSerializable(typeof(Plugins.PluginConfiguration))]
[JsonSerializable(typeof(Plugins.CliProviderConfiguration))]
[JsonSerializable(typeof(Plugins.CliToolConfiguration))]
[JsonSerializable(typeof(List<Plugins.CliProviderConfiguration>))]
[JsonSerializable(typeof(List<Plugins.CliToolConfiguration>))]
[JsonSerializable(typeof(Common.Contracts.ParameterEntry))]
[JsonSerializable(typeof(List<Common.Contracts.ParameterEntry>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
public partial class McpHostContext : JsonSerializerContext
{
}
