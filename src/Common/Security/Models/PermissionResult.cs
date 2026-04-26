using Common.Contracts;

using SecurityConstants = Common.Constants.ConstantManager.Security;

namespace Common.Security.Models;

/// <summary>
/// 权限检查结果
/// 使用 ValidationResult 作为基础，统一验证结果类型
/// </summary>
public sealed class PermissionResult : IValidationResult
{
    /// <summary>
    /// 是否允许执行
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// 拒绝原因
    /// </summary>
    public string? DenyReason { get; init; }

    /// <summary>
    /// 缺失的权限
    /// </summary>
    public IReadOnlyList<string> MissingPermissions { get; init; } = [];

    /// <inheritdoc />
    public bool IsValid => IsAllowed;

    /// <inheritdoc />
    public IReadOnlyList<string> Errors => IsAllowed ? [] : [DenyReason ?? "权限不足"];

    /// <summary>
    /// 创建允许结果
    /// </summary>
    public static PermissionResult Allowed() => new() { IsAllowed = true };

    /// <summary>
    /// 创建拒绝结果
    /// </summary>
    public static PermissionResult Denied(string reason, params string[] missingPermissions) =>
        new() { IsAllowed = false, DenyReason = reason, MissingPermissions = missingPermissions };

    /// <summary>
    /// 从 ValidationResult 创建权限结果
    /// </summary>
    public static PermissionResult FromValidationResult(ValidationResult result)
    {
        if (result.IsValid)
        {
            return Allowed();
        }

        return Denied(
            result.Errors.FirstOrDefault() ?? "验证失败",
            result.DetectedAttacks.ToArray());
    }

    /// <summary>
    /// 转换为 ValidationResult
    /// </summary>
    public ValidationResult ToValidationResult()
    {
        if (IsAllowed)
        {
            return ValidationResultFactory.Success();
        }

        var builder = ValidationResultFactory.Builder().WithErrors(Errors);
        if (MissingPermissions.Count > 0)
        {
            builder.WithAttacks(MissingPermissions);
        }

        return builder.Build();
    }
}
