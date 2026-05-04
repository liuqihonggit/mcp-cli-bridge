using Common.IO;

namespace McpHost.PluginManager;

/// <summary>
/// 插件配置验证结果
/// </summary>
public sealed class PluginValidationResult
{
    /// <summary>
    /// 是否验证通过
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// 验证错误列表
    /// </summary>
    public IReadOnlyList<PluginValidationError> Errors { get; init; } = [];

    /// <summary>
    /// 验证警告列表
    /// </summary>
    public IReadOnlyList<PluginValidationWarning> Warnings { get; init; } = [];

    /// <summary>
    /// 创建成功的验证结果
    /// </summary>
    public static PluginValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// 创建失败的验证结果
    /// </summary>
    public static PluginValidationResult Failure(
        IReadOnlyList<PluginValidationError> errors,
        IReadOnlyList<PluginValidationWarning>? warnings = null)
        => new()
        {
            IsValid = false,
            Errors = errors,
            Warnings = warnings ?? []
        };
}

/// <summary>
/// 插件验证错误
/// </summary>
public sealed class PluginValidationError
{
    /// <summary>
    /// 错误代码
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 错误位置（提供者名称或工具名称）
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// 创建验证错误
    /// </summary>
    public static PluginValidationError Create(string code, string message, string? location = null)
        => new() { Code = code, Message = message, Location = location };
}

/// <summary>
/// 插件验证警告
/// </summary>
public sealed class PluginValidationWarning
{
    /// <summary>
    /// 警告代码
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// 警告消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 警告位置
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// 创建验证警告
    /// </summary>
    public static PluginValidationWarning Create(string code, string message, string? location = null)
        => new() { Code = code, Message = message, Location = location };
}

/// <summary>
/// 插件配置验证器
/// 验证插件配置文件的正确性和完整性
/// </summary>
public sealed class PluginConfigurationValidator
{
    private readonly ILogger _logger;
    private readonly HashSet<string> _registeredToolNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 验证错误代码常量
    /// </summary>
    public static class ErrorCodes
    {
        public const string MissingVersion = "MISSING_VERSION";
        public const string InvalidVersion = "INVALID_VERSION";
        public const string MissingProviders = "MISSING_PROVIDERS";
        public const string EmptyProviders = "EMPTY_PROVIDERS";
        public const string MissingProviderType = "MISSING_PROVIDER_TYPE";
        public const string MissingProviderName = "MISSING_PROVIDER_NAME";
        public const string DuplicateProviderName = "DUPLICATE_PROVIDER_NAME";
        public const string MissingToolName = "MISSING_TOOL_NAME";
        public const string MissingToolDescription = "MISSING_TOOL_DESCRIPTION";
        public const string DuplicateToolName = "DUPLICATE_TOOL_NAME";
        public const string InvalidTimeout = "INVALID_TIMEOUT";
        public const string InvalidProcessPoolSize = "INVALID_PROCESS_POOL_SIZE";
        public const string InvalidInputSchema = "INVALID_INPUT_SCHEMA";
        public const string MissingExecutablePath = "MISSING_EXECUTABLE_PATH";
    }

    /// <summary>
    /// 验证警告代码常量
    /// </summary>
    public static class WarningCodes
    {
        public const string DefaultTimeout = "DEFAULT_TIMEOUT";
        public const string LargeProcessPool = "LARGE_PROCESS_POOL";
        public const string NoRequiredPermissions = "NO_REQUIRED_PERMISSIONS";
        public const string EmptyTools = "EMPTY_TOOLS";
    }

    public PluginConfigurationValidator(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 验证插件配置
    /// </summary>
    /// <param name="configuration">插件配置</param>
    /// <returns>验证结果</returns>
    public PluginValidationResult Validate(PluginConfiguration? configuration)
    {
        if (configuration is null)
        {
            return PluginValidationResult.Failure([
                PluginValidationError.Create(
                    ErrorCodes.MissingProviders,
                    "Plugin configuration is null")
            ]);
        }

        var errors = new List<PluginValidationError>();
        var warnings = new List<PluginValidationWarning>();
        _registeredToolNames.Clear();

        // 验证版本
        ValidateVersion(configuration, errors);

        // 验证提供者列表
        ValidateProviders(configuration, errors, warnings);

        if (errors.Count > 0)
        {
            _logger.Log(LogLevel.Error, $"Plugin configuration validation failed with {errors.Count} errors");
            return PluginValidationResult.Failure([.. errors], [.. warnings]);
        }

        if (warnings.Count > 0)
        {
            _logger.Log(LogLevel.Warn, $"Plugin configuration validation passed with {warnings.Count} warnings");
        }
        else
        {
            _logger.Log(LogLevel.Info, "Plugin configuration validation passed");
        }

        return new PluginValidationResult
        {
            IsValid = true,
            Warnings = [.. warnings]
        };
    }

    /// <summary>
    /// 验证配置文件是否存在并有效
    /// </summary>
    /// <param name="configPath">配置文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PluginValidationResult> ValidateFileAsync(string configPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(configPath))
        {
            return PluginValidationResult.Failure([
                PluginValidationError.Create(
                    "FILE_NOT_FOUND",
                    $"Plugin configuration file not found: {configPath}")
            ]);
        }

        try
        {
            var configuration = await FileHelper.ReadJsonAsync(
                configPath,
                McpHostContext.Default.PluginConfiguration,
                cancellationToken);
            return Validate(configuration);
        }
        catch (JsonException ex)
        {
            return PluginValidationResult.Failure([
                PluginValidationError.Create(
                    "JSON_PARSE_ERROR",
                    $"Failed to parse plugin configuration: {ex.Message}")
            ]);
        }
        catch (Exception ex)
        {
            return PluginValidationResult.Failure([
                PluginValidationError.Create(
                    "FILE_READ_ERROR",
                    $"Failed to read plugin configuration: {ex.Message}")
            ]);
        }
    }

    private static void ValidateVersion(PluginConfiguration configuration, List<PluginValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(configuration.Version))
        {
            errors.Add(PluginValidationError.Create(
                ErrorCodes.MissingVersion,
                "Plugin configuration version is missing"));
            return;
        }

        if (!IsValidVersion(configuration.Version))
        {
            errors.Add(PluginValidationError.Create(
                ErrorCodes.InvalidVersion,
                $"Invalid version format: {configuration.Version}. Expected format: major.minor.patch"));
        }
    }

    private void ValidateProviders(
        PluginConfiguration configuration,
        List<PluginValidationError> errors,
        List<PluginValidationWarning> warnings)
    {
        if (configuration.Providers is null)
        {
            errors.Add(PluginValidationError.Create(
                ErrorCodes.MissingProviders,
                "Providers list is null"));
            return;
        }

        if (configuration.Providers.Count == 0)
        {
            errors.Add(PluginValidationError.Create(
                ErrorCodes.EmptyProviders,
                "Providers list is empty"));
            return;
        }

        var providerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in configuration.Providers)
        {
            ValidateProvider(provider, providerNames, errors, warnings);
        }
    }

    private void ValidateProvider(
        CliProviderConfiguration provider,
        HashSet<string> providerNames,
        List<PluginValidationError> errors,
        List<PluginValidationWarning> warnings)
    {
        var location = provider.Name ?? "[unnamed]";

        // 验证提供者类型
        if (string.IsNullOrWhiteSpace(provider.Type))
        {
            errors.Add(PluginValidationError.Create(
                ErrorCodes.MissingProviderType,
                "Provider type is missing",
                location));
        }

        // 验证提供者名称
        if (string.IsNullOrWhiteSpace(provider.Name))
        {
            errors.Add(PluginValidationError.Create(
                ErrorCodes.MissingProviderName,
                "Provider name is missing"));
        }
        else if (!providerNames.Add(provider.Name))
        {
            errors.Add(PluginValidationError.Create(
                ErrorCodes.DuplicateProviderName,
                $"Duplicate provider name: {provider.Name}",
                location));
        }

        // 验证可执行路径
        if (string.IsNullOrWhiteSpace(provider.ExecutablePath) &&
            string.IsNullOrWhiteSpace(provider.CliCommand))
        {
            errors.Add(PluginValidationError.Create(
                ErrorCodes.MissingExecutablePath,
                "Provider must have either executablePath or cliCommand",
                location));
        }

        // 验证超时时间
        if (!string.IsNullOrWhiteSpace(provider.Timeout))
        {
            if (!TimeSpan.TryParse(provider.Timeout, out var timeout) || timeout.TotalSeconds <= 0)
            {
                errors.Add(PluginValidationError.Create(
                    ErrorCodes.InvalidTimeout,
                    $"Invalid timeout format: {provider.Timeout}",
                    location));
            }
        }
        else
        {
            warnings.Add(PluginValidationWarning.Create(
                WarningCodes.DefaultTimeout,
                "Using default timeout of 30 seconds",
                location));
        }

        // 验证进程池大小
        if (provider.ProcessPoolSize <= 0)
        {
            errors.Add(PluginValidationError.Create(
                ErrorCodes.InvalidProcessPoolSize,
                $"Invalid process pool size: {provider.ProcessPoolSize}. Must be greater than 0",
                location));
        }
        else if (provider.ProcessPoolSize > 20)
        {
            warnings.Add(PluginValidationWarning.Create(
                WarningCodes.LargeProcessPool,
                $"Large process pool size ({provider.ProcessPoolSize}) may consume significant resources",
                location));
        }

        // 验证工具列表
        ValidateTools(provider, errors, warnings);
    }

    private void ValidateTools(
        CliProviderConfiguration provider,
        List<PluginValidationError> errors,
        List<PluginValidationWarning> warnings)
    {
        var location = provider.Name ?? "[unnamed]";

        if (provider.Tools is null || provider.Tools.Count == 0)
        {
            warnings.Add(PluginValidationWarning.Create(
                WarningCodes.EmptyTools,
                "Provider has no tools defined, will discover via CLI protocol at startup",
                location));
            return;
        }

        foreach (var tool in provider.Tools)
        {
            ValidateTool(tool, location, errors, warnings);
        }
    }

    private void ValidateTool(
        CliToolConfiguration tool,
        string providerName,
        List<PluginValidationError> errors,
        List<PluginValidationWarning> warnings)
    {
        var location = $"{providerName}/{tool.Name ?? "[unnamed]"}";

        // 验证工具名称
        if (string.IsNullOrWhiteSpace(tool.Name))
        {
            errors.Add(PluginValidationError.Create(
                ErrorCodes.MissingToolName,
                "Tool name is missing",
                providerName));
            return;
        }

        // 检查工具名称重复
        if (!_registeredToolNames.Add(tool.Name))
        {
            errors.Add(PluginValidationError.Create(
                ErrorCodes.DuplicateToolName,
                $"Duplicate tool name: {tool.Name}",
                location));
        }

        // 验证工具描述
        if (string.IsNullOrWhiteSpace(tool.Description))
        {
            errors.Add(PluginValidationError.Create(
                ErrorCodes.MissingToolDescription,
                $"Tool '{tool.Name}' is missing description",
                location));
        }

        // 验证输入模式
        if (tool.InputSchema.ValueKind == JsonValueKind.Undefined ||
            tool.InputSchema.ValueKind == JsonValueKind.Null)
        {
            errors.Add(PluginValidationError.Create(
                ErrorCodes.InvalidInputSchema,
                $"Tool '{tool.Name}' is missing inputSchema",
                location));
        }

        // 验证权限
        if (tool.RequiredPermissions is null || tool.RequiredPermissions.Count == 0)
        {
            warnings.Add(PluginValidationWarning.Create(
                WarningCodes.NoRequiredPermissions,
                $"Tool '{tool.Name}' has no required permissions defined",
                location));
        }
    }

    private static bool IsValidVersion(string version)
    {
        var parts = version.Split('.');
        if (parts.Length < 2 || parts.Length > 3)
            return false;

        return parts.All(p => int.TryParse(p, out _));
    }
}
