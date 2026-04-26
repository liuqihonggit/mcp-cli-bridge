using System.Text.Json.Serialization.Metadata;

namespace Service.Json;

/// <summary>
/// MCP JSON 序列化器 - AOT 兼容
/// </summary>
public static class McpJsonSerializer
{
    /// <summary>
    /// 序列化对象
    /// </summary>
    public static string Serialize<T>(T value) where T : notnull
    {
        var typeInfo = CommonJsonContext.Default.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>;
        return typeInfo == null
            ? throw new InvalidOperationException($"Type info not found for {typeof(T)}")
            : JsonSerializer.Serialize(value, typeInfo);
    }

    /// <summary>
    /// 序列化对象（动态类型）
    /// </summary>
    public static string SerializeObject(object value)
    {
        return SerializeObjectInternal(value);
    }

    private static string SerializeObjectInternal(object value)
    {
        if (value is null) return "null";
        if (value is string s) return JsonSerializer.Serialize(s, CommonJsonContext.Default.String);
        if (value is int i) return i.ToString();
        if (value is long l) return l.ToString();
        if (value is double d) return d.ToString();
        if (value is float f) return f.ToString();
        if (value is bool b) return b.ToString().ToLowerInvariant();
        if (value is JsonElement element) return element.GetRawText();
        if (value is Dictionary<string, JsonElement> dict) return JsonSerializer.Serialize(dict, CommonJsonContext.Default.DictionaryStringJsonElement);
        if (value is List<ToolContent> list) return JsonSerializer.Serialize(list, CommonJsonContext.Default.ListToolContent);
        if (value is ToolContent content) return JsonSerializer.Serialize(content, CommonJsonContext.Default.ToolContent);
        if (value is CallToolResult result) return JsonSerializer.Serialize(result, CommonJsonContext.Default.CallToolResult);
        if (value is ListToolsResult listResult) return JsonSerializer.Serialize(listResult, CommonJsonContext.Default.ListToolsResult);

        return JsonSerializer.Serialize(value.ToString(), CommonJsonContext.Default.String);
    }

    /// <summary>
    /// 反序列化对象
    /// </summary>
    public static T? Deserialize<T>(string json)
    {
        var typeInfo = CommonJsonContext.Default.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>;
        return typeInfo == null
            ? throw new InvalidOperationException($"Type info not found for {typeof(T)}")
            : JsonSerializer.Deserialize(json, typeInfo);
    }

}
