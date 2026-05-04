using System.Text.Json.Serialization;

namespace Common.Contracts;

/// <summary>
/// 通用操作结果类，统一所有响应/结果类型
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public sealed class OperationResult<T>
{
    /// <summary>
    /// 操作是否成功
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>
    /// 是否为回退结果（当资源被锁定时使用备用路径）
    /// </summary>
    [JsonPropertyName("isFallback")]
    public bool IsFallback => Metadata?.TryGetValue("isFallback", out var value) == true && value is true;

    /// <summary>
    /// 消息（成功或错误信息）
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 输出内容（CLI执行结果，与 Message 相同）
    /// </summary>
    [JsonPropertyName("output")]
    public string Output => Message;

    /// <summary>
    /// 数据
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; init; }

    /// <summary>
    /// 扩展元数据
    /// </summary>
    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// 进程退出代码（CLI执行相关）
    /// </summary>
    [JsonPropertyName("exitCode")]
    public int ExitCode { get; init; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    [JsonPropertyName("executionTimeMs")]
    public double ExecutionTimeMs { get; init; }

    /// <summary>
    /// 错误详情（null 时不序列化）
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    /// <summary>
    /// 隐式转换为布尔值
    /// </summary>
    public static implicit operator bool(OperationResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Success;
    }

    /// <summary>
    /// 转换为布尔值的显式方法（运算符备用）
    /// </summary>
    public bool ToBoolean() => Success;
}

/// <summary>
/// 非泛型操作结果类
/// </summary>
public sealed class OperationResult
{
    /// <summary>
    /// 操作是否成功
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>
    /// 是否为回退结果（当资源被锁定时使用备用路径）
    /// </summary>
    [JsonPropertyName("isFallback")]
    public bool IsFallback => Metadata?.TryGetValue("isFallback", out var value) == true && value is true;

    /// <summary>
    /// 消息
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 输出内容（CLI执行结果，与 Message 相同）
    /// </summary>
    [JsonPropertyName("output")]
    public string Output => Message;

    /// <summary>
    /// 扩展元数据
    /// </summary>
    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// 进程退出代码（CLI执行相关）
    /// </summary>
    [JsonPropertyName("exitCode")]
    public int ExitCode { get; init; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    [JsonPropertyName("executionTimeMs")]
    public double ExecutionTimeMs { get; init; }

    /// <summary>
    /// 错误详情
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    /// <summary>
    /// 隐式转换为布尔值
    /// </summary>
    public static implicit operator bool(OperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Success;
    }

    /// <summary>
    /// 转换为布尔值的显式方法（运算符备用）
    /// </summary>
    public bool ToBoolean() => Success;
}
