namespace Common.Contracts;

/// <summary>
/// 验证结果公共接口
/// 统一所有验证结果类型的契约
/// </summary>
public interface IValidationResult
{
    /// <summary>
    /// 是否验证通过
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// 验证错误信息列表
    /// </summary>
    IReadOnlyList<string> Errors { get; }
}

/// <summary>
/// 验证结果接口（带警告支持）
/// </summary>
public interface IValidationResultWithWarnings : IValidationResult
{
    /// <summary>
    /// 警告信息列表
    /// </summary>
    IReadOnlyList<string> Warnings { get; }
}

/// <summary>
/// 安全验证结果接口
/// </summary>
public interface ISecurityValidationResult : IValidationResult
{
    /// <summary>
    /// 检测到的攻击类型列表
    /// </summary>
    IReadOnlyList<string> DetectedAttacks { get; }
}
