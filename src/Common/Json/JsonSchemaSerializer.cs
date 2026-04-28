namespace Common.Json;

/// <summary>
/// JSON Schema 序列化器
/// </summary>
public static class JsonSchemaSerializer
{
    /// <summary>
    /// 将 Schema 序列化为 JsonElement
    /// </summary>
    public static JsonElement SerializeToJsonElement(JsonSchema schema)
    {
        var json = JsonSerializer.Serialize(schema, CommonJsonContext.Default.JsonSchema);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    /// <summary>
    /// 将 Schema 属性序列化为 JsonElement
    /// </summary>
    public static JsonElement SerializeToJsonElement(JsonSchemaProperty property)
    {
        var json = JsonSerializer.Serialize(property, CommonJsonContext.Default.JsonSchemaProperty);
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
