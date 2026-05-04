using SecurityConstants = Common.Constants.ConstantManager.Security;

namespace Common.Security.Logging;

/// <summary>
/// 安全审计日志记录器实现 - 使用 SecurityAuditEntryBuilder 重构
/// </summary>
public sealed class SecurityAuditLogger : ISecurityLogger
{
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<SecurityAuditEntry> _auditLogQueue;
    private readonly int _maxQueueSize;

    public SecurityAuditLogger(ILogger logger, int maxQueueSize = 10000)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxQueueSize = maxQueueSize;
        _auditLogQueue = new ConcurrentQueue<SecurityAuditEntry>();
    }

    public Task LogInputValidationFailedAsync(
        string toolName,
        IReadOnlyList<string> errors,
        IReadOnlyList<string> detectedAttacks,
        string? userId = null)
    {
        var entry = SecurityAuditEntryBuilder
            .Create()
            .ForInputValidationFailed()
            .ForTool(toolName)
            .WithUser(userId)
            .Failed($"输入验证失败: {errors.Count} 个错误")
            .WithData("errors", errors.ToList())
            .WithAttacks(detectedAttacks)
            .Build();

        return LogAuditEntryAsync(entry);
    }

    public Task LogPermissionDeniedAsync(
        string toolName,
        string? userId,
        string reason,
        IReadOnlyList<string> missingPermissions)
    {
        var entry = SecurityAuditEntryBuilder
            .Create()
            .ForPermissionDenied()
            .ForTool(toolName)
            .WithUser(userId)
            .Failed($"权限拒绝: {reason}")
            .WithData("reason", reason)
            .WithData("missingPermissions", missingPermissions.ToList())
            .Build();

        return LogAuditEntryAsync(entry);
    }

    public Task LogMaliciousContentDetectedAsync(
        string toolName,
        string attackType,
        string content,
        string? userId = null)
    {
        var entry = SecurityAuditEntryBuilder
            .Create()
            .ForMaliciousContentDetected()
            .ForTool(toolName)
            .WithUser(userId)
            .Failed($"检测到恶意内容: {attackType}")
            .WithData("attackType", attackType)
            .WithData("contentPreview", content.Length > 200 ? content[..200] + "..." : content)
            .WithAttack(attackType)
            .Build();

        return LogAuditEntryAsync(entry);
    }

    public Task LogToolExecutionBlockedAsync(
        string toolName,
        string reason,
        string? userId = null)
    {
        var entry = SecurityAuditEntryBuilder
            .Create()
            .ForToolExecutionBlocked()
            .ForTool(toolName)
            .WithUser(userId)
            .Failed($"工具执行被阻止: {reason}")
            .WithData("reason", reason)
            .Build();

        return LogAuditEntryAsync(entry);
    }

    public Task LogAuditEntryAsync(SecurityAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _auditLogQueue.Enqueue(entry);

        while (_auditLogQueue.Count > _maxQueueSize && _auditLogQueue.TryDequeue(out _))
        {
        }

        var logLevel = entry.IsSuccess ? LogLevel.Info : LogLevel.Warn;
        var logMessage = $"[{entry.EventType}] Tool={entry.ToolName}, User={entry.UserId ?? "anonymous"}, Success={entry.IsSuccess}, Message={entry.Message}";

        _logger.Log(logLevel, logMessage);

        if (!entry.IsSuccess && IsCriticalSecurityEvent(entry.EventType))
        {
            var detailedMessage = $"安全事件详情: {JsonSerializer.Serialize(entry, CommonJsonContext.Default.SecurityAuditEntry)}";
            _logger.Log(LogLevel.Error, detailedMessage);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SecurityAuditEntry>> GetAuditLogsAsync(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string? eventType = null)
    {
        var logs = _auditLogQueue
            .Where(entry => entry.Timestamp >= startTime && entry.Timestamp <= endTime)
            .Where(entry => eventType is null || entry.EventType == eventType)
            .OrderBy(entry => entry.Timestamp)
            .ToList();

        return Task.FromResult<IReadOnlyList<SecurityAuditEntry>>(logs);
    }

    private static bool IsCriticalSecurityEvent(string eventType)
    {
        // 使用常量字符串进行比较，避免在 switch 表达式中使用非常量值
        const string maliciousContent = "MALICIOUS_CONTENT_DETECTED";
        const string unauthorizedAccess = "UNAUTHORIZED_ACCESS";
        const string whitelistViolation = "WHITELIST_VIOLATION";

        return eventType == maliciousContent
            || eventType == unauthorizedAccess
            || eventType == whitelistViolation;
    }
}

/// <summary>
/// 安全审计日志条目构建器
/// </summary>
public sealed class SecurityAuditEntryBuilder
{
    private string _eventType = string.Empty;
    private string _toolName = string.Empty;
    private string? _userId;
    private bool _isSuccess = true;
    private string _message = string.Empty;
    private readonly Dictionary<string, object> _additionalData = [];
    private readonly List<string> _detectedAttacks = [];

    /// <summary>
    /// 创建新的构建器实例
    /// </summary>
    public static SecurityAuditEntryBuilder Create() => new();

    /// <summary>
    /// 设置事件类型
    /// </summary>
    public SecurityAuditEntryBuilder ForEvent(string eventType)
    {
        _eventType = eventType;
        return this;
    }

    /// <summary>
    /// 设置工具名称
    /// </summary>
    public SecurityAuditEntryBuilder ForTool(string toolName)
    {
        _toolName = toolName;
        return this;
    }

    /// <summary>
    /// 设置用户ID
    /// </summary>
    public SecurityAuditEntryBuilder WithUser(string? userId)
    {
        _userId = userId;
        return this;
    }

    /// <summary>
    /// 标记为成功
    /// </summary>
    public SecurityAuditEntryBuilder Succeeded(string message)
    {
        _isSuccess = true;
        _message = message;
        return this;
    }

    /// <summary>
    /// 标记为失败
    /// </summary>
    public SecurityAuditEntryBuilder Failed(string message)
    {
        _isSuccess = false;
        _message = message;
        return this;
    }

    /// <summary>
    /// 添加附加数据
    /// </summary>
    public SecurityAuditEntryBuilder WithData(string key, object value)
    {
        _additionalData[key] = value;
        return this;
    }

    /// <summary>
    /// 添加多个附加数据
    /// </summary>
    public SecurityAuditEntryBuilder WithData(Dictionary<string, object> data)
    {
        foreach (var kvp in data)
        {
            _additionalData[kvp.Key] = kvp.Value;
        }
        return this;
    }

    /// <summary>
    /// 添加检测到的攻击
    /// </summary>
    public SecurityAuditEntryBuilder WithAttack(string attackType)
    {
        _detectedAttacks.Add(attackType);
        return this;
    }

    /// <summary>
    /// 添加多个检测到的攻击
    /// </summary>
    public SecurityAuditEntryBuilder WithAttacks(IEnumerable<string> attacks)
    {
        _detectedAttacks.AddRange(attacks);
        return this;
    }

    /// <summary>
    /// 构建审计日志条目
    /// </summary>
    public SecurityAuditEntry Build()
    {
        return new SecurityAuditEntry
        {
            EventType = _eventType,
            ToolName = _toolName,
            UserId = _userId,
            IsSuccess = _isSuccess,
            Message = _message,
            AdditionalData = _additionalData.Count > 0 ? new Dictionary<string, object>(_additionalData) : new Dictionary<string, object>(),
            DetectedAttacks = _detectedAttacks.Count > 0 ? new List<string>(_detectedAttacks) : new List<string>()
        };
    }

    // 预定义的事件类型快捷方法

    /// <summary>
    /// 输入验证失败事件
    /// </summary>
    public SecurityAuditEntryBuilder ForInputValidationFailed()
    {
        return ForEvent(SecurityConstants.EventTypes.InputValidationFailed);
    }

    /// <summary>
    /// 权限拒绝事件
    /// </summary>
    public SecurityAuditEntryBuilder ForPermissionDenied()
    {
        return ForEvent(SecurityConstants.EventTypes.PermissionDenied);
    }

    /// <summary>
    /// 恶意内容检测事件
    /// </summary>
    public SecurityAuditEntryBuilder ForMaliciousContentDetected()
    {
        return ForEvent(SecurityConstants.EventTypes.MaliciousContentDetected);
    }

    /// <summary>
    /// 工具执行被阻止事件
    /// </summary>
    public SecurityAuditEntryBuilder ForToolExecutionBlocked()
    {
        return ForEvent(SecurityConstants.EventTypes.ToolExecutionBlocked);
    }

    /// <summary>
    /// 未授权访问事件
    /// </summary>
    public SecurityAuditEntryBuilder ForUnauthorizedAccess()
    {
        return ForEvent(SecurityConstants.EventTypes.UnauthorizedAccess);
    }

    /// <summary>
    /// 白名单违规事件
    /// </summary>
    public SecurityAuditEntryBuilder ForWhitelistViolation()
    {
        return ForEvent(SecurityConstants.EventTypes.WhitelistViolation);
    }
}
