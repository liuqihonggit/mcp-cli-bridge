using Common.Contracts;

using SecurityConstants = Common.Constants.ConstantManager.Security;

namespace Common.Security.Services;

/// <summary>
/// 安全服务接口，提供统一的安全管理功能
/// </summary>
public interface ISecurityService
{
    /// <summary>
    /// 验证输入参数
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="parameters">参数字典</param>
    /// <param name="inputSchema">输入Schema</param>
    /// <returns>验证结果</returns>
    Task<ValidationResult> ValidateInputAsync(
        string toolName,
        IReadOnlyDictionary<string, JsonElement> parameters,
        JsonElement inputSchema);

    /// <summary>
    /// 检查权限
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="userId">用户ID</param>
    /// <param name="requiredPermissions">所需权限</param>
    /// <returns>权限检查结果</returns>
    Task<PermissionResult> CheckPermissionAsync(
        string toolName,
        string? userId,
        IReadOnlyList<string> requiredPermissions);

    /// <summary>
    /// 记录安全事件
    /// </summary>
    /// <param name="entry">审计条目</param>
    /// <returns>异步任务</returns>
    Task LogSecurityEventAsync(SecurityAuditEntry entry);

    /// <summary>
    /// 获取审计日志
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="eventType">事件类型（可选）</param>
    /// <returns>审计日志列表</returns>
    Task<IReadOnlyList<SecurityAuditEntry>> GetAuditLogsAsync(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string? eventType = null);
}

/// <summary>
/// 安全服务实现
/// </summary>
public sealed class SecurityService : ISecurityService
{
    private readonly IInputValidator _inputValidator;
    private readonly IPermissionChecker _permissionChecker;
    private readonly ISecurityLogger _securityLogger;

    /// <summary>
    /// 初始化安全服务
    /// </summary>
    /// <param name="inputValidator">输入验证器</param>
    /// <param name="permissionChecker">权限检查器</param>
    /// <param name="securityLogger">安全日志记录器</param>
    public SecurityService(
        IInputValidator inputValidator,
        IPermissionChecker permissionChecker,
        ISecurityLogger securityLogger)
    {
        _inputValidator = inputValidator ?? throw new ArgumentNullException(nameof(inputValidator));
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
        _securityLogger = securityLogger ?? throw new ArgumentNullException(nameof(securityLogger));
    }

    /// <inheritdoc />
    public async Task<ValidationResult> ValidateInputAsync(
        string toolName,
        IReadOnlyDictionary<string, JsonElement> parameters,
        JsonElement inputSchema)
    {
        var request = new InputValidationRequest
        {
            ToolName = toolName,
            Parameters = parameters,
            InputSchema = inputSchema
        };

        var result = await _inputValidator.ValidateAsync(request);

        if (!result.IsValid)
        {
            await _securityLogger.LogInputValidationFailedAsync(
                toolName,
                result.Errors,
                result.DetectedAttacks);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<PermissionResult> CheckPermissionAsync(
        string toolName,
        string? userId,
        IReadOnlyList<string> requiredPermissions)
    {
        var request = new PermissionCheckRequest
        {
            ToolName = toolName,
            UserId = userId,
            RequiredPermissions = requiredPermissions
        };

        var result = await _permissionChecker.CheckPermissionAsync(request);

        if (!result.IsAllowed)
        {
            await _securityLogger.LogPermissionDeniedAsync(
                toolName,
                userId,
                result.DenyReason ?? "权限不足",
                result.MissingPermissions);
        }

        return result;
    }

    /// <inheritdoc />
    public Task LogSecurityEventAsync(SecurityAuditEntry entry)
    {
        return _securityLogger.LogAuditEntryAsync(entry);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SecurityAuditEntry>> GetAuditLogsAsync(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string? eventType = null)
    {
        return _securityLogger.GetAuditLogsAsync(startTime, endTime, eventType);
    }
}
