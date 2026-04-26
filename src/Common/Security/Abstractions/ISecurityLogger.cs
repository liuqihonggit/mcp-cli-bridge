using SecurityConstants = Common.Constants.ConstantManager.Security;

namespace Common.Security.Abstractions;

/// <summary>
/// 安全日志记录器接口，负责记录安全相关事件
/// </summary>
public interface ISecurityLogger
{
    /// <summary>
    /// 记录输入验证失败
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="errors">验证错误列表</param>
    /// <param name="detectedAttacks">检测到的攻击类型列表</param>
    /// <param name="userId">用户ID</param>
    /// <returns>异步任务</returns>
    Task LogInputValidationFailedAsync(
        string toolName,
        IReadOnlyList<string> errors,
        IReadOnlyList<string> detectedAttacks,
        string? userId = null);

    /// <summary>
    /// 记录权限拒绝
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="userId">用户ID</param>
    /// <param name="reason">拒绝原因</param>
    /// <param name="missingPermissions">缺失的权限</param>
    /// <returns>异步任务</returns>
    Task LogPermissionDeniedAsync(
        string toolName,
        string? userId,
        string reason,
        IReadOnlyList<string> missingPermissions);

    /// <summary>
    /// 记录恶意内容检测
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="attackType">攻击类型</param>
    /// <param name="content">恶意内容</param>
    /// <param name="userId">用户ID</param>
    /// <returns>异步任务</returns>
    Task LogMaliciousContentDetectedAsync(
        string toolName,
        string attackType,
        string content,
        string? userId = null);

    /// <summary>
    /// 记录工具执行阻止
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="reason">阻止原因</param>
    /// <param name="userId">用户ID</param>
    /// <returns>异步任务</returns>
    Task LogToolExecutionBlockedAsync(
        string toolName,
        string reason,
        string? userId = null);

    /// <summary>
    /// 记录安全审计条目
    /// </summary>
    /// <param name="entry">审计条目</param>
    /// <returns>异步任务</returns>
    Task LogAuditEntryAsync(SecurityAuditEntry entry);

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
