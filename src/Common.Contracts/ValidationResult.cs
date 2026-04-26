namespace Common.Contracts;

/// <summary>
/// 统一的验证结果类
/// 支持错误、警告和攻击检测
/// </summary>
public sealed class ValidationResult : ISecurityValidationResult, IValidationResultWithWarnings
{
    /// <inheritdoc />
    public bool IsValid { get; init; }

    /// <inheritdoc />
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <inheritdoc />
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <inheritdoc />
    public IReadOnlyList<string> DetectedAttacks { get; init; } = [];
}
