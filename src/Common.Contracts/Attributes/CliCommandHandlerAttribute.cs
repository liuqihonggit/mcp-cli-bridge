namespace Common.Contracts.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CliCommandHandlerAttribute : Attribute
{
    public string PluginName { get; }

    public string Description { get; }

    public string Category { get; set; } = "general";

    public string ToolNamePrefix { get; set; } = "";

    public bool HasDocumentation { get; set; }

    public CliCommandHandlerAttribute(string pluginName, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginName);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        PluginName = pluginName;
        Description = description;
    }
}
