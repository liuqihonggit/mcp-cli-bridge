using SecurityConstants = Common.Constants.ConstantManager.Security;

namespace Common.Security;

/// <summary>
/// 安全验证服务接口
/// 提供输入验证、权限检查和恶意内容检测
/// </summary>
public interface ISecurityValidator
{
    /// <summary>
    /// 验证输入参数
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="parameters">输入参数</param>
    /// <returns>验证结果</returns>
    SecurityValidationResult ValidateInput(string toolName, IReadOnlyDictionary<string, JsonElement> parameters);

    /// <summary>
    /// 检查工具执行权限
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="context">执行上下文</param>
    /// <returns>权限检查结果</returns>
    Task<Models.PermissionResult> CheckPermissionAsync(string toolName, SecurityContext context);

    /// <summary>
    /// 检查工具是否在白名单中
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <returns>是否在白名单中</returns>
    bool IsToolAllowed(string toolName);

    /// <summary>
    /// 检测恶意内容
    /// </summary>
    /// <param name="content">待检测内容</param>
    /// <returns>检测结果</returns>
    MaliciousContentResult DetectMaliciousContent(string content);
}

/// <summary>
/// 安全验证结果
/// </summary>
public sealed class SecurityValidationResult
{
    /// <summary>
    /// 是否验证通过
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// 验证错误列表
    /// </summary>
    public IReadOnlyList<SecurityValidationError> Errors { get; init; } = [];

    /// <summary>
    /// 创建成功的验证结果
    /// </summary>
    public static SecurityValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// 创建失败的验证结果
    /// </summary>
    public static SecurityValidationResult Failure(params SecurityValidationError[] errors) =>
        new() { IsValid = false, Errors = errors };
}

/// <summary>
/// 安全验证错误信息
/// </summary>
public sealed class SecurityValidationError
{
    /// <summary>
    /// 错误字段
    /// </summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 创建验证错误
    /// </summary>
    public static SecurityValidationError Create(string field, string message) =>
        new() { Field = field, Message = message };
}


/// 恶意内容检测结果
/// </summary>
public sealed class MaliciousContentResult
{
    /// <summary>
    /// 是否检测到恶意内容
    /// </summary>
    public bool IsMalicious { get; init; }

    /// <summary>
    /// 检测到的攻击类型
    /// </summary>
    public IReadOnlyList<string> DetectedTypes { get; init; } = [];

    /// <summary>
    /// 匹配的模式
    /// </summary>
    public IReadOnlyList<string> MatchedPatterns { get; init; } = [];

    /// <summary>
    /// 创建安全结果
    /// </summary>
    public static MaliciousContentResult Safe() => new() { IsMalicious = false };

    /// <summary>
    /// 创建恶意内容结果
    /// </summary>
    public static MaliciousContentResult Malicious(IEnumerable<string> types, IEnumerable<string> patterns) =>
        new() { IsMalicious = true, DetectedTypes = types.ToList(), MatchedPatterns = patterns.ToList() };
}

/// <summary>
/// 安全上下文
/// </summary>
public sealed record SecurityContext
{
    /// <summary>
    /// 用户标识
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// 角色列表
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// 权限列表
    /// </summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>
    /// 来源
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// 创建默认上下文
    /// </summary>
    public static SecurityContext Default => new();
}
