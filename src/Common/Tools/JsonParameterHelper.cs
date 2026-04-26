using Common.Contracts;

namespace Common.Tools;

/// <summary>
/// JSON 参数帮助类，统一处理参数的序列化和反序列化
/// </summary>
public static class JsonParameterHelper
{
    /// <summary>
    /// 将参数字典序列化为日志字符串
    /// </summary>
    /// <param name="parameters">参数字典</param>
    /// <returns>序列化后的字符串</returns>
    public static string SerializeForLog(Dictionary<string, JsonElement> parameters)
    {
        if (parameters.Count == 0)
        {
            return "{}";
        }

        try
        {
            var items = new List<ParameterEntry>(parameters.Count);
            foreach (var kvp in parameters)
            {
                var value = ConvertJsonElementToString(kvp.Value);
                items.Add(new ParameterEntry { Key = kvp.Key, Value = value });
            }

            return JsonSerializer.Serialize(items, CommonJsonContext.Default.ListParameterEntry);
        }
        catch
        {
            return "(参数序列化失败)";
        }
    }

    /// <summary>
    /// 将 JsonElement 转换为字符串
    /// </summary>
    /// <param name="element">JSON 元素</param>
    /// <returns>字符串表示</returns>
    public static string ConvertJsonElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "null",
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            JsonValueKind.Array => $"[数组({element.GetArrayLength()})]",
            JsonValueKind.Object => "{对象}",
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// 反序列化参数到目标类型
    /// </summary>
    /// <param name="value">JSON 元素</param>
    /// <param name="targetType">目标类型</param>
    /// <returns>反序列化后的值</returns>
    public static object? DeserializeArgument(JsonElement value, Type targetType)
    {
        if (targetType == typeof(string))
            return value.GetString();

        if (targetType == typeof(int) || targetType == typeof(int?))
            return value.GetInt32();

        if (targetType == typeof(long) || targetType == typeof(long?))
            return value.GetInt64();

        if (targetType == typeof(double) || targetType == typeof(double?))
            return value.GetDouble();

        if (targetType == typeof(float) || targetType == typeof(float?))
            return value.GetSingle();

        if (targetType == typeof(bool) || targetType == typeof(bool?))
            return value.GetBoolean();

        if (targetType == typeof(Dictionary<string, JsonElement>))
            return JsonSerializer.Deserialize(value.GetRawText(), CommonJsonContext.Default.DictionaryStringJsonElement);

        if (targetType == typeof(JsonElement))
            return value;

        // 对于其他类型，返回 JsonElement
        return value;
    }

    /// <summary>
    /// 反序列化参数数组
    /// </summary>
    /// <param name="parameters">参数字典</param>
    /// <param name="parameterInfos">参数信息列表</param>
    /// <returns>参数值数组</returns>
    public static object?[] DeserializeArguments(
        Dictionary<string, JsonElement> parameters,
        IReadOnlyList<ParameterInfo> parameterInfos)
    {
        if (parameterInfos.Count == 0) return [];

        var result = new object?[parameterInfos.Count];
        for (int i = 0; i < parameterInfos.Count; i++)
        {
            var param = parameterInfos[i];
            if (parameters.TryGetValue(param.Name, out var value))
            {
                result[i] = DeserializeArgument(value, param.Type);
            }
            else
            {
                result[i] = param.DefaultValue;
            }
        }

        return result;
    }

    /// <summary>
    /// 序列化结果为 JSON 字符串
    /// </summary>
    /// <param name="result">结果对象</param>
    /// <returns>JSON 字符串</returns>
    public static string SerializeResult(object? result)
    {
        return result switch
        {
            null => "null",
            string s => s,
            Dictionary<string, JsonElement> dict => JsonSerializer.Serialize(dict, CommonJsonContext.Default.DictionaryStringJsonElement),
            JsonElement element => element.GetRawText(),
            _ => SerializeObject(result)
        };
    }

    /// <summary>
    /// 使用 Utf8JsonWriter 序列化对象
    /// </summary>
    private static string SerializeObject(object result)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WritePropertyName("result");
        writer.WriteStringValue(result.ToString());
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// 安全地获取 JSON 属性值
    /// </summary>
    /// <param name="element">JSON 元素</param>
    /// <param name="propertyName">属性名</param>
    /// <returns>属性值，如果不存在则返回 null</returns>
    public static JsonElement? GetPropertySafe(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (element.TryGetProperty(propertyName, out var property))
            return property;

        return null;
    }

    /// <summary>
    /// 安全地获取字符串属性值
    /// </summary>
    /// <param name="element">JSON 元素</param>
    /// <param name="propertyName">属性名</param>
    /// <returns>字符串值，如果不存在则返回 null</returns>
    public static string? GetStringProperty(JsonElement element, string propertyName)
    {
        var property = GetPropertySafe(element, propertyName);
        return property?.GetString();
    }

    /// <summary>
    /// 参数信息
    /// </summary>
    public sealed class ParameterInfo
    {
        public string Name { get; init; } = string.Empty;
        public Type Type { get; init; } = typeof(object);
        public object? DefaultValue { get; init; }
    }
}
