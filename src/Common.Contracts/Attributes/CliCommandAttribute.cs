namespace Common.Contracts.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class CliCommandAttribute : Attribute
{
    public string Name { get; }

    public string Description { get; set; } = "";

    public string Category { get; set; } = "general";

    public Type? SchemaType { get; set; }

    public CliCommandAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
}
