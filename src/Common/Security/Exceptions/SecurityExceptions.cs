using SecurityConstants = Common.Constants.ConstantManager.Security;

namespace Common.Security.Exceptions;

/// <summary>
/// 安全异常基类
/// </summary>
public abstract class SecurityException : DomainException
{
    protected SecurityException(string errorCode, string message)
        : base(errorCode, message)
    {
    }

    protected SecurityException(string errorCode, string message, Exception innerException)
        : base(errorCode, message, innerException)
    {
    }
}

/// <summary>
/// 输入验证异常
/// </summary>
public sealed class InputValidationException : SecurityException
{
    public IReadOnlyList<string> ValidationErrors { get; }

    public InputValidationException(IEnumerable<string> errors)
        : base(SecurityConstants.EventTypes.InputValidationFailed, string.Join("; ", errors))
    {
        ValidationErrors = errors.ToList().AsReadOnly();
    }

    public InputValidationException(string error)
        : base(SecurityConstants.EventTypes.InputValidationFailed, error)
    {
        ValidationErrors = new List<string> { error }.AsReadOnly();
    }
}

/// <summary>
/// 权限拒绝异常
/// </summary>
public sealed class PermissionDeniedException : SecurityException
{
    public IReadOnlyList<string> MissingPermissions { get; }

    public PermissionDeniedException(string reason, params string[] missingPermissions)
        : base(SecurityConstants.EventTypes.PermissionDenied, reason)
    {
        MissingPermissions = missingPermissions.ToList().AsReadOnly();
    }
}

/// <summary>
/// 恶意内容检测异常
/// </summary>
public sealed class MaliciousContentException : SecurityException
{
    public string AttackType { get; }

    public MaliciousContentException(string attackType, string description)
        : base(SecurityConstants.EventTypes.MaliciousContentDetected, $"检测到恶意内容: {attackType} - {description}")
    {
        AttackType = attackType;
    }
}

/// <summary>
/// 白名单违规异常
/// </summary>
public sealed class WhitelistViolationException : SecurityException
{
    public string ToolName { get; }

    public WhitelistViolationException(string toolName)
        : base(SecurityConstants.EventTypes.WhitelistViolation, $"工具 '{toolName}' 不在白名单中")
    {
        ToolName = toolName;
    }
}

/// <summary>
/// 未授权访问异常
/// </summary>
public sealed class UnauthorizedAccessException : SecurityException
{
    public UnauthorizedAccessException(string message)
        : base(SecurityConstants.EventTypes.UnauthorizedAccess, message)
    {
    }
}
