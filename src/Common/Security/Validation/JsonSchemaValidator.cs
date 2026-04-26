using Common.Contracts;
using Common.Constants;

using SecurityConstants = Common.Constants.ConstantManager.Security;

namespace Common.Security.Validation;

/// <summary>
/// JSON Schema验证器实现
/// </summary>
public sealed class JsonSchemaValidator : IInputValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <inheritdoc />
    public Task<ValidationResult> ValidateAsync(
        InputValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();
        var detectedAttacks = new List<string>();

        // 验证Schema
        var schemaResult = ValidateSchema(request.Parameters, request.InputSchema);
        if (!schemaResult.IsValid)
        {
            errors.AddRange(schemaResult.Errors);
        }

        // 验证参数类型
        var typeResult = ValidateParameterTypes(request.Parameters, request.InputSchema);
        if (!typeResult.IsValid)
        {
            errors.AddRange(typeResult.Errors);
        }

        // 检测恶意内容
        var maliciousResult = DetectMaliciousContentInParameters(request.Parameters);
        if (!maliciousResult.IsValid)
        {
            errors.AddRange(maliciousResult.Errors);
            detectedAttacks.AddRange(maliciousResult.DetectedAttacks);
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = errors,
                DetectedAttacks = detectedAttacks
            });
        }

        return Task.FromResult(errors.Count == 0
            ? ValidationResultFactory.Success()
            : ValidationResultFactory.Failure(errors));
    }

    /// <inheritdoc />
    public ValidationResult ValidateSchema(
        IReadOnlyDictionary<string, JsonElement> parameters,
        JsonElement schema)
    {
        var errors = new List<string>();

        if (!schema.TryGetProperty("required", out var requiredProps) ||
            requiredProps.ValueKind != JsonValueKind.Array)
        {
            return ValidationResultFactory.Success();
        }

        var requiredList = requiredProps.EnumerateArray()
            .Select(prop => prop.GetString())
            .Where(name => name is not null)
            .Select(name => name!)
            .ToList();

        var missingRequired = requiredList
            .Where(required => !parameters.ContainsKey(required))
            .ToList();

        errors.AddRange(missingRequired.Select(missing =>
            string.Format(ConstantManager.ValidationMessages.MissingRequiredParameter, missing)));

        return errors.Count == 0
            ? ValidationResultFactory.Success()
            : ValidationResultFactory.Failure(errors);
    }

    /// <inheritdoc />
    public ValidationResult DetectMaliciousContent(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return ValidationResultFactory.Success();
        }

        // 检查长度限制
        if (content.Length > SecurityConstants.Limits.MaxStringLength)
        {
            return ValidationResultFactory.Failure(
                $"内容长度超过限制: {content.Length} > {SecurityConstants.Limits.MaxStringLength}");
        }

        // 检测SQL注入
        var sqlInjectionResult = DetectSqlInjection(content);
        if (!sqlInjectionResult.IsValid)
        {
            return sqlInjectionResult;
        }

        // 检测命令注入
        var commandInjectionResult = DetectCommandInjection(content);
        if (!commandInjectionResult.IsValid)
        {
            return commandInjectionResult;
        }

        // 检测XSS
        var xssResult = DetectXss(content);
        if (!xssResult.IsValid)
        {
            return xssResult;
        }

        // 检测路径遍历
        var pathTraversalResult = DetectPathTraversal(content);
        if (!pathTraversalResult.IsValid)
        {
            return pathTraversalResult;
        }

        return ValidationResultFactory.Success();
    }

    /// <inheritdoc />
    public ValidationResult ValidateParameterTypes(
        IReadOnlyDictionary<string, JsonElement> parameters,
        JsonElement schema)
    {
        var errors = new List<string>();

        if (!schema.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object)
        {
            return ValidationResultFactory.Success();
        }

        foreach (var param in parameters)
        {
            if (!properties.TryGetProperty(param.Key, out var propSchema))
            {
                continue;
            }

            var typeError = ValidateParameterType(param.Key, param.Value, propSchema);
            if (typeError is not null)
            {
                errors.Add(typeError);
            }
        }

        return errors.Count == 0
            ? ValidationResultFactory.Success()
            : ValidationResultFactory.Failure(errors);
    }

    /// <summary>
    /// 验证单个参数类型
    /// </summary>
    private static string? ValidateParameterType(
        string paramName,
        JsonElement value,
        JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var typeElement))
        {
            return null;
        }

        var expectedType = typeElement.GetString();
        if (string.IsNullOrEmpty(expectedType))
        {
            return null;
        }

        var actualKind = value.ValueKind;

        bool isValid;
        if (expectedType == JsonValueTypes.String)
            isValid = actualKind == JsonValueKind.String;
        else if (expectedType == JsonValueTypes.Integer)
            isValid = actualKind == JsonValueKind.Number;
        else if (expectedType == JsonValueTypes.Number)
            isValid = actualKind == JsonValueKind.Number;
        else if (expectedType == JsonValueTypes.Boolean)
            isValid = actualKind == JsonValueKind.True || actualKind == JsonValueKind.False;
        else if (expectedType == JsonValueTypes.Array)
            isValid = actualKind == JsonValueKind.Array;
        else if (expectedType == JsonValueTypes.Object)
            isValid = actualKind == JsonValueKind.Object;
        else
            isValid = true;

        return !isValid
            ? string.Format(ConstantManager.ValidationMessages.ParameterTypeMismatch, expectedType, actualKind)
            : null;
    }

    /// <summary>
    /// 检测参数中的恶意内容
    /// </summary>
    private ValidationResult DetectMaliciousContentInParameters(
        IReadOnlyDictionary<string, JsonElement> parameters)
    {
        foreach (var param in parameters)
        {
            var content = ExtractStringContent(param.Value);
            if (string.IsNullOrEmpty(content))
            {
                continue;
            }

            var result = DetectMaliciousContent(content);
            if (!result.IsValid)
            {
                return result;
            }
        }

        return ValidationResultFactory.Success();
    }

    /// <summary>
    /// 从JsonElement中提取字符串内容
    /// </summary>
    private static string? ExtractStringContent(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True or JsonValueKind.False => element.GetBoolean().ToString(),
            JsonValueKind.Object or JsonValueKind.Array => element.GetRawText(),
            _ => null
        };
    }

    /// <summary>
    /// 检测SQL注入
    /// </summary>
    private static ValidationResult DetectSqlInjection(string content)
    {
        foreach (var pattern in SecurityConstants.MaliciousPatterns.SqlInjectionPatterns)
        {
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
            {
                return ValidationResultFactory.MaliciousContentDetected(
                    SecurityConstants.AttackTypes.SqlInjection,
                    $"检测到SQL注入模式: {pattern}");
            }
        }

        return ValidationResultFactory.Success();
    }

    /// <summary>
    /// 检测命令注入
    /// </summary>
    private static ValidationResult DetectCommandInjection(string content)
    {
        foreach (var pattern in SecurityConstants.MaliciousPatterns.CommandInjectionPatterns)
        {
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
            {
                return ValidationResultFactory.MaliciousContentDetected(
                    SecurityConstants.AttackTypes.CommandInjection,
                    $"检测到命令注入模式: {pattern}");
            }
        }

        return ValidationResultFactory.Success();
    }

    /// <summary>
    /// 检测XSS攻击
    /// </summary>
    private static ValidationResult DetectXss(string content)
    {
        foreach (var pattern in SecurityConstants.MaliciousPatterns.XssPatterns)
        {
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
            {
                return ValidationResultFactory.MaliciousContentDetected(
                    SecurityConstants.AttackTypes.Xss,
                    $"检测到XSS攻击模式: {pattern}");
            }
        }

        return ValidationResultFactory.Success();
    }

    /// <summary>
    /// 检测路径遍历攻击
    /// </summary>
    private static ValidationResult DetectPathTraversal(string content)
    {
        foreach (var pattern in SecurityConstants.MaliciousPatterns.PathTraversalPatterns)
        {
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
            {
                return ValidationResultFactory.MaliciousContentDetected(
                    SecurityConstants.AttackTypes.PathTraversal,
                    $"检测到路径遍历模式: {pattern}");
            }
        }

        return ValidationResultFactory.Success();
    }
}
