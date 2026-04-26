using System.Text.Json.Serialization;

namespace Common.Contracts;

/// <summary>
/// 参数条目（用于日志序列化）
/// </summary>
public sealed class ParameterEntry
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}
