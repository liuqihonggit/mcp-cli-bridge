using Common.IO;

namespace McpHost.PluginManager;

public sealed class PluginConfigurationLoader
{
    private readonly ILogger _logger;
    private readonly IPackageManager _packageManager;
    private readonly IProcessPoolManager _processPoolManager;
    private readonly PluginConfigurationValidator _validator;

    public PluginConfigurationLoader(
        ILogger logger,
        IPackageManager packageManager,
        IProcessPoolManager processPoolManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _packageManager = packageManager ?? throw new ArgumentNullException(nameof(packageManager));
        _processPoolManager = processPoolManager ?? throw new ArgumentNullException(nameof(processPoolManager));
        _validator = new PluginConfigurationValidator(logger);
    }

    public async Task<IReadOnlyList<IToolProvider>> LoadProvidersAsync(string configPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(configPath))
        {
            await _logger.LogAsync(LogLevel.Warn, $"Plugin configuration file not found: {configPath}", cancellationToken);
            return [];
        }

        try
        {
            var configuration = await FileHelper.ReadJsonAsync(
                configPath,
                McpHostContext.Default.PluginConfiguration,
                cancellationToken);

            if (configuration?.Providers == null || configuration.Providers.Count == 0)
            {
                await _logger.LogAsync(LogLevel.Warn, "No providers found in configuration file", cancellationToken);
                return [];
            }

            var validationResult = _validator.Validate(configuration);
            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    await _logger.LogAsync(LogLevel.Error, $"Validation error [{error.Code}]: {error.Message}" +
                        (error.Location != null ? $" at {error.Location}" : ""), cancellationToken);
                }
                return [];
            }

            foreach (var warning in validationResult.Warnings)
            {
                await _logger.LogAsync(LogLevel.Warn, $"Validation warning [{warning.Code}]: {warning.Message}" +
                    (warning.Location != null ? $" at {warning.Location}" : ""), cancellationToken);
            }

            var providers = new List<IToolProvider>();

            foreach (var providerConfig in configuration.Providers)
            {
                var provider = CreateProvider(providerConfig);
                if (provider is null)
                    continue;

                if (providerConfig.Tools.Count == 0)
                {
                    await _logger.LogAsync(LogLevel.Info, $"No tools defined in config for {provider.ProviderName}, discovering via CLI protocol...", cancellationToken);
                    var discovered = await provider.DiscoverToolsAsync();
                    if (!discovered)
                    {
                        await _logger.LogAsync(LogLevel.Warn, $"Tool discovery failed for {provider.ProviderName}, provider will have no tools", cancellationToken);
                    }
                }

                providers.Add(provider);
                await _logger.LogAsync(LogLevel.Info, $"Loaded provider: {provider.ProviderName}", cancellationToken);
            }

            return providers;
        }
        catch (Exception ex)
        {
            await _logger.LogAsync(LogLevel.Error, ex, $"Failed to load plugin configuration from {configPath}", cancellationToken);
            return [];
        }
    }

    public async Task<bool> SaveConfigurationAsync(PluginConfiguration configuration, string configPath, CancellationToken cancellationToken = default)
    {
        try
        {
            await FileHelper.WriteJsonAsync(
                configPath,
                configuration,
                McpHostContext.Default.PluginConfiguration,
                cancellationToken);
            await _logger.LogAsync(LogLevel.Info, $"Saved plugin configuration to {configPath}", cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            await _logger.LogAsync(LogLevel.Error, ex, $"Failed to save plugin configuration to {configPath}", cancellationToken);
            return false;
        }
    }

    private CliToolProvider CreateProvider(CliProviderConfiguration config)
    {
        return new CliToolProvider(_logger, _packageManager, _processPoolManager, config);
    }
}
