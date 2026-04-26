using Common.Contracts;

namespace Common.Results;

/// <summary>
/// ValidationResult 工厂类，用于创建各种验证结果
/// </summary>
public static class ValidationResultFactory
{
    /// <summary>
    /// 创建成功的验证结果
    /// </summary>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// 创建失败的验证结果
    /// </summary>
    public static ValidationResult Failure(params string[] errors) => new() { IsValid = false, Errors = errors };

    /// <summary>
    /// 创建失败的验证结果（从错误集合）
    /// </summary>
    public static ValidationResult Failure(IEnumerable<string> errors) => new() { IsValid = false, Errors = errors.ToList() };

    /// <summary>
    /// 创建带警告的验证结果
    /// </summary>
    public static ValidationResult WithWarnings(params string[] warnings) => new() { IsValid = true, Warnings = warnings };

    /// <summary>
    /// 创建恶意内容检测结果
    /// </summary>
    public static ValidationResult MaliciousContentDetected(string attackType, string description) =>
        new()
        {
            IsValid = false,
            Errors = [$"检测到恶意内容: {attackType} - {description}"],
            DetectedAttacks = [attackType]
        };

    /// <summary>
    /// 创建构建器
    /// </summary>
    public static ValidationResultBuilder Builder() => new();
}

/// <summary>
/// 验证结果构建器
/// </summary>
public sealed class ValidationResultBuilder
{
    private readonly List<string> _errors = [];
    private readonly List<string> _warnings = [];
    private readonly List<string> _detectedAttacks = [];

    /// <summary>
    /// 添加错误
    /// </summary>
    public ValidationResultBuilder WithError(string error)
    {
        _errors.Add(error);
        return this;
    }

    /// <summary>
    /// 添加多个错误
    /// </summary>
    public ValidationResultBuilder WithErrors(IEnumerable<string> errors)
    {
        _errors.AddRange(errors);
        return this;
    }

    /// <summary>
    /// 添加警告
    /// </summary>
    public ValidationResultBuilder WithWarning(string warning)
    {
        _warnings.Add(warning);
        return this;
    }

    /// <summary>
    /// 添加多个警告
    /// </summary>
    public ValidationResultBuilder WithWarnings(IEnumerable<string> warnings)
    {
        _warnings.AddRange(warnings);
        return this;
    }

    /// <summary>
    /// 添加攻击类型
    /// </summary>
    public ValidationResultBuilder WithAttack(string attackType)
    {
        _detectedAttacks.Add(attackType);
        return this;
    }

    /// <summary>
    /// 添加多个攻击类型
    /// </summary>
    public ValidationResultBuilder WithAttacks(IEnumerable<string> attackTypes)
    {
        _detectedAttacks.AddRange(attackTypes);
        return this;
    }

    /// <summary>
    /// 构建验证结果
    /// </summary>
    public ValidationResult Build()
    {
        return new ValidationResult
        {
            IsValid = _errors.Count == 0,
            Errors = _errors,
            Warnings = _warnings,
            DetectedAttacks = _detectedAttacks
        };
    }
}
