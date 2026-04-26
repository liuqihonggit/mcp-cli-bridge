using SecurityConstants = Common.Constants.ConstantManager.Security;

namespace Common.Security;

/// <summary>
/// 安全验证服务实现
/// 整合输入验证和权限检查
/// </summary>
public sealed class SecurityValidator : ISecurityValidator
{
    private readonly IInputValidator _inputValidator;
    private readonly IPermissionChecker _permissionChecker;
    private readonly WhitelistConfiguration _whitelist;

    /// <summary>
    /// 初始化安全验证器
    /// </summary>
    public SecurityValidator(
        IInputValidator inputValidator,
        IPermissionChecker permissionChecker,
        WhitelistConfiguration? whitelist = null)
    {
        _inputValidator = inputValidator ?? throw new ArgumentNullException(nameof(inputValidator));
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
        _whitelist = whitelist ?? new WhitelistConfiguration { IsEnabled = false };
    }

    /// <inheritdoc />
    public SecurityValidationResult ValidateInput(string toolName, IReadOnlyDictionary<string, JsonElement> parameters)
    {
        ArgumentNullException.ThrowIfNull(toolName);
        ArgumentNullException.ThrowIfNull(parameters);

        var errors = new List<SecurityValidationError>();

        // 检查参数数量限制
        if (parameters.Count > SecurityConstants.Limits.MaxParameterCount)
        {
            errors.Add(SecurityValidationError.Create(
                nameof(parameters),
                $"参数数量超过限制: {parameters.Count} > {SecurityConstants.Limits.MaxParameterCount}"));
        }

        // 验证每个参数
        foreach (var (key, value) in parameters)
        {
            var paramResult = ValidateParameter(key, value);
            if (!paramResult.IsValid)
            {
                errors.AddRange(paramResult.Errors);
            }
        }

        return errors.Count == 0
            ? SecurityValidationResult.Success()
            : SecurityValidationResult.Failure([.. errors]);
    }

    /// <inheritdoc />
    public async Task<PermissionResult> CheckPermissionAsync(string toolName, SecurityContext context)
    {
        ArgumentNullException.ThrowIfNull(toolName);

        // 检查白名单
        if (_whitelist.IsEnabled && !_whitelist.AllowedTools.Contains(toolName))
        {
            return PermissionResult.Denied(
                $"工具 '{toolName}' 不在白名单中",
                SecurityConstants.EventTypes.WhitelistViolation);
        }

        // 如果没有用户信息，使用默认权限检查
        if (string.IsNullOrEmpty(context.UserId))
        {
            return PermissionResult.Allowed();
        }

        // 构建权限检查请求
        var request = new PermissionCheckRequest
        {
            ToolName = toolName,
            UserId = context.UserId,
            Roles = context.Roles,
            RequiredPermissions = []
        };

        // 使用权限检查器
        return await _permissionChecker.CheckPermissionAsync(request).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public bool IsToolAllowed(string toolName)
    {
        ArgumentNullException.ThrowIfNull(toolName);

        if (!_whitelist.IsEnabled)
        {
            return true;
        }

        return _whitelist.AllowedTools.Contains(toolName);
    }

    /// <inheritdoc />
    public MaliciousContentResult DetectMaliciousContent(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var detectedTypes = new List<string>();
        var matchedPatterns = new List<string>();

        // 检查SQL注入
        foreach (var pattern in SecurityConstants.MaliciousPatterns.SqlInjectionPatterns)
        {
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled))
            {
                detectedTypes.Add(SecurityConstants.AttackTypes.SqlInjection);
                matchedPatterns.Add(pattern);
            }
        }

        // 检查命令注入
        foreach (var pattern in SecurityConstants.MaliciousPatterns.CommandInjectionPatterns)
        {
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled))
            {
                detectedTypes.Add(SecurityConstants.AttackTypes.CommandInjection);
                matchedPatterns.Add(pattern);
            }
        }

        // 检查XSS
        foreach (var pattern in SecurityConstants.MaliciousPatterns.XssPatterns)
        {
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled))
            {
                detectedTypes.Add(SecurityConstants.AttackTypes.Xss);
                matchedPatterns.Add(pattern);
            }
        }

        // 检查路径遍历
        foreach (var pattern in SecurityConstants.MaliciousPatterns.PathTraversalPatterns)
        {
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled))
            {
                detectedTypes.Add(SecurityConstants.AttackTypes.PathTraversal);
                matchedPatterns.Add(pattern);
            }
        }

        // 检查环境变量注入
        foreach (var pattern in SecurityConstants.MaliciousPatterns.EnvironmentVariablePatterns)
        {
            if (Regex.IsMatch(content, pattern, RegexOptions.Compiled))
            {
                detectedTypes.Add(SecurityConstants.AttackTypes.CommandInjection);
                matchedPatterns.Add(pattern);
            }
        }

        // 检查多行命令注入 (换行符后跟危险命令)
        var lines = content.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 1)
        {
            var dangerousCommands = new[] { "rm", "del", "rmdir", "format", "mkfs", "dd" };
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                foreach (var cmd in dangerousCommands)
                {
                    if (line.StartsWith(cmd + " ", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith(cmd + "\t", StringComparison.OrdinalIgnoreCase))
                    {
                        detectedTypes.Add(SecurityConstants.AttackTypes.CommandInjection);
                        matchedPatterns.Add($"multiline_{cmd}");
                    }
                }
            }
        }

        return detectedTypes.Count == 0
            ? MaliciousContentResult.Safe()
            : MaliciousContentResult.Malicious(detectedTypes, matchedPatterns);
    }

    private SecurityValidationResult ValidateParameter(string key, JsonElement value)
    {
        var errors = new List<SecurityValidationError>();

        // 检查字符串长度
        if (value.ValueKind == JsonValueKind.String)
        {
            var stringValue = value.GetString() ?? string.Empty;
            if (stringValue.Length > SecurityConstants.Limits.MaxStringLength)
            {
                errors.Add(SecurityValidationError.Create(
                    key,
                    $"字符串长度超过限制: {stringValue.Length} > {SecurityConstants.Limits.MaxStringLength}"));
            }

            // 检测恶意内容
            var maliciousResult = DetectMaliciousContent(stringValue);
            if (maliciousResult.IsMalicious)
            {
                errors.Add(SecurityValidationError.Create(
                    key,
                    $"检测到恶意内容: {string.Join(", ", maliciousResult.DetectedTypes)}"));
            }
        }

        // 检查数组长度
        if (value.ValueKind == JsonValueKind.Array)
        {
            var arrayLength = value.GetArrayLength();
            if (arrayLength > SecurityConstants.Limits.MaxArrayLength)
            {
                errors.Add(SecurityValidationError.Create(
                    key,
                    $"数组长度超过限制: {arrayLength} > {SecurityConstants.Limits.MaxArrayLength}"));
            }
        }

        return errors.Count == 0
            ? SecurityValidationResult.Success()
            : SecurityValidationResult.Failure([.. errors]);
    }
}
