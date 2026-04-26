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
