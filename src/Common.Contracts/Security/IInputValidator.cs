namespace Common.Contracts.Security;

/// <summary>
/// 输入验证器接口，负责验证工具输入参数
/// </summary>
public interface IInputValidator
{
    /// <summary>
    /// 异步验证输入参数
    /// </summary>
    /// <param name="request">验证请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>验证结果</returns>
    Task<ValidationResult> ValidateAsync(
        InputValidationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证JSON Schema
    /// </summary>
    /// <param name="parameters">参数字典</param>
    /// <param name="schema">JSON Schema</param>
    /// <returns>验证结果</returns>
    ValidationResult ValidateSchema(
        IReadOnlyDictionary<string, JsonElement> parameters,
        JsonElement schema);

    /// <summary>
    /// 检测恶意内容
    /// </summary>
    /// <param name="content">待检测内容</param>
    /// <returns>检测结果</returns>
    ValidationResult DetectMaliciousContent(string content);

    /// <summary>
    /// 验证参数类型
    /// </summary>
    /// <param name="parameters">参数字典</param>
    /// <param name="schema">JSON Schema</param>
    /// <returns>验证结果</returns>
    ValidationResult ValidateParameterTypes(
        IReadOnlyDictionary<string, JsonElement> parameters,
        JsonElement schema);
}
