namespace Common.Tools;

public sealed class ToolsManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = Versions.McpHost;

    [JsonPropertyName("tools")]
    public List<ToolManifestDefinition> Tools { get; set; } = [];
}

public sealed class ToolManifestDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("cliCommand")]
    public string CliCommand { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = "general";

    [JsonPropertyName("defaultTimeout")]
    public int DefaultTimeout { get; set; } = 30000;

    [JsonPropertyName("requiredPermissions")]
    public List<string> RequiredPermissions { get; set; } = [];

    [JsonPropertyName("inputSchema")]
    public InputSchemaDefinition InputSchema { get; set; } = new();

    [JsonPropertyName("enhancedDescription")]
    public EnhancedToolDescription? EnhancedDescription { get; set; }

    /// <summary>
    /// 获取完整的工具描述，包含增强描述信息
    /// </summary>
    public string GetFullDescription()
    {
        if (EnhancedDescription is null)
            return Description;

        return $"{Description}\n\n{EnhancedDescription.GenerateFullDescription()}";
    }
}

public sealed class InputSchemaDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = JsonValueTypes.ObjectType;

    [JsonPropertyName("properties")]
    public Dictionary<string, PropertyDefinition> Properties { get; set; } = [];

    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = [];
}

public sealed class PropertyDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = JsonValueTypes.StringType;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Default { get; set; }

    [JsonPropertyName("const")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Const { get; set; }

    [JsonPropertyName("items")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ItemsDefinition? Items { get; set; }
}

public sealed class ItemsDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = JsonValueTypes.StringType;

    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, PropertyDefinition>? Properties { get; set; }

    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Required { get; set; }

    [JsonPropertyName("items")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ItemsDefinition? Items { get; set; }
}
