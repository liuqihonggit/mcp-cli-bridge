namespace Service.Json.Contracts;

/// <summary>
/// 验证错误详情
/// </summary>
public sealed class ValidationError
{
    /// <summary>
    /// 错误字段路径
    /// </summary>
    [JsonPropertyName("fieldPath")]
    public string FieldPath { get; init; } = string.Empty;

    /// <summary>
    /// 错误字段（别名，兼容旧代码）
    /// </summary>
    [JsonPropertyName("field")]
    public string Field => FieldPath;

    /// <summary>
    /// 错误消息
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 错误代码
    /// </summary>
    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>
    /// 错误代码（别名，兼容旧代码）
    /// </summary>
    [JsonPropertyName("code")]
    public string Code => ErrorCode;

    /// <summary>
    /// 创建验证错误
    /// </summary>
    public ValidationError() { }

    /// <summary>
    /// 创建验证错误
    /// </summary>
    public ValidationError(string fieldPath, string message, string errorCode = "VALIDATION_ERROR")
    {
        FieldPath = fieldPath;
        Message = message;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// 安全审计日志条目
/// </summary>
public sealed class SecurityAuditEntry
{
    /// <summary>
    /// 时间戳
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 事件类型
    /// </summary>
    [JsonPropertyName("eventType")]
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// 工具名称
    /// </summary>
    [JsonPropertyName("toolName")]
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// 用户ID
    /// </summary>
    [JsonPropertyName("userId")]
    public string? UserId { get; init; }

    /// <summary>
    /// 是否成功
    /// </summary>
    [JsonPropertyName("isSuccess")]
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 详细消息
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 额外数据
    /// </summary>
    [JsonPropertyName("additionalData")]
    public Dictionary<string, object> AdditionalData { get; init; } = [];

    /// <summary>
    /// 来源IP
    /// </summary>
    [JsonPropertyName("sourceIp")]
    public string? SourceIp { get; init; }

    /// <summary>
    /// 请求ID
    /// </summary>
    [JsonPropertyName("requestId")]
    public string? RequestId { get; init; }

    /// <summary>
    /// 检测到的攻击类型列表
    /// </summary>
    [JsonPropertyName("detectedAttacks")]
    public IReadOnlyList<string> DetectedAttacks { get; init; } = [];
}

/// <summary>
/// 白名单配置
/// </summary>
public sealed class WhitelistConfiguration
{
    /// <summary>
    /// 允许的工具列表
    /// </summary>
    [JsonPropertyName("allowedTools")]
    public HashSet<string> AllowedTools { get; init; } = [];

    /// <summary>
    /// 允许的CLI命令列表
    /// </summary>
    [JsonPropertyName("allowedCliCommands")]
    public HashSet<string> AllowedCliCommands { get; init; } = [];

    /// <summary>
    /// 是否启用白名单
    /// </summary>
    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; init; } = true;
}

/// <summary>
/// RBAC角色配置
/// </summary>
public sealed class RbacConfiguration
{
    /// <summary>
    /// 角色权限映射
    /// </summary>
    [JsonPropertyName("rolePermissions")]
    public Dictionary<string, HashSet<string>> RolePermissions { get; init; } = [];

    /// <summary>
    /// 用户角色映射
    /// </summary>
    [JsonPropertyName("userRoles")]
    public Dictionary<string, HashSet<string>> UserRoles { get; init; } = [];

    /// <summary>
    /// 是否启用RBAC
    /// </summary>
    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; init; } = true;
}

/// <summary>
/// 安全配置JSON模型 - 用于序列化
/// </summary>
public sealed class WhitelistConfigurationJsonModel
{
    [JsonPropertyName("allowedTools")]
    public List<string> AllowedTools { get; set; } = [];

    [JsonPropertyName("allowedCliCommands")]
    public List<string> AllowedCliCommands { get; set; } = [];

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// RBAC配置JSON模型 - 用于序列化
/// </summary>
public sealed class RbacConfigurationJsonModel
{
    [JsonPropertyName("rolePermissions")]
    public Dictionary<string, List<string>> RolePermissions { get; set; } = [];

    [JsonPropertyName("userRoles")]
    public Dictionary<string, List<string>> UserRoles { get; set; } = [];

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;
}
