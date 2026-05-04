namespace Common.Contracts;

public interface IValidationResult
{
    bool IsValid { get; }
    IReadOnlyList<string> Errors { get; }
}

public interface IValidationResultWithWarnings : IValidationResult
{
    IReadOnlyList<string> Warnings { get; }
}

public interface ISecurityValidationResult : IValidationResult
{
    IReadOnlyList<string> DetectedAttacks { get; }
}

public sealed class ValidationResult : ISecurityValidationResult, IValidationResultWithWarnings
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> DetectedAttacks { get; init; } = [];
}
