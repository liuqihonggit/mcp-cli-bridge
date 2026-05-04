using System.Text.Json.Serialization.Metadata;
using McpProtocol.Contracts;

namespace Common.Json;

public static class McpJsonSerializer
{
    /// <summary>
    /// 空对象 JsonElement（{}）
    /// </summary>
    public static JsonElement EmptyObject { get; }

    static McpJsonSerializer()
    {
        using var doc = JsonDocument.Parse("{}");
        EmptyObject = doc.RootElement.Clone();
    }

    public static string Serialize<T>(T value) where T : notnull
    {
        var typeInfo = CommonJsonContext.Default.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>;
        return typeInfo == null
            ? throw new InvalidOperationException($"Type info not found for {typeof(T)}")
            : JsonSerializer.Serialize(value, typeInfo);
    }

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
        if (value is List<ToolContent> list) return JsonSerializer.Serialize(list, McpJsonContext.Default.ListToolContent);
        if (value is ToolContent content) return JsonSerializer.Serialize(content, McpJsonContext.Default.ToolContent);
        if (value is CallToolResult result) return JsonSerializer.Serialize(result, McpJsonContext.Default.CallToolResult);
        if (value is ListToolsResult listResult) return JsonSerializer.Serialize(listResult, McpJsonContext.Default.ListToolsResult);

        return JsonSerializer.Serialize(value.ToString(), CommonJsonContext.Default.String);
    }

    public static T? Deserialize<T>(string json)
    {
        var typeInfo = CommonJsonContext.Default.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>;
        return typeInfo == null
            ? throw new InvalidOperationException($"Type info not found for {typeof(T)}")
            : JsonSerializer.Deserialize(json, typeInfo);
    }
}
