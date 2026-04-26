using Common.Tools;

namespace McpHost.Plugins;

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
            _logger.Log(LogLevel.Warn, $"Plugin configuration file not found: {configPath}");
            return [];
        }

        try
        {
            var configuration = await FileOperationHelper.ReadJsonAsync(
                configPath,
                McpHostContext.Default.PluginConfiguration,
                cancellationToken);

            if (configuration?.Providers == null || configuration.Providers.Count == 0)
            {
                _logger.Log(LogLevel.Warn, "No providers found in configuration file");
                return [];
            }

            var validationResult = _validator.Validate(configuration);
            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    _logger.Log(LogLevel.Error, $"Validation error [{error.Code}]: {error.Message}" +
                        (error.Location != null ? $" at {error.Location}" : ""));
                }
                return [];
            }

            foreach (var warning in validationResult.Warnings)
            {
                _logger.Log(LogLevel.Warn, $"Validation warning [{warning.Code}]: {warning.Message}" +
                    (warning.Location != null ? $" at {warning.Location}" : ""));
            }

            var providers = new List<IToolProvider>();

            foreach (var providerConfig in configuration.Providers)
            {
                var provider = CreateProvider(providerConfig);
                if (provider is null)
                    continue;

                if (providerConfig.Tools.Count == 0)
                {
                    _logger.Log(LogLevel.Info, $"No tools defined in config for {provider.ProviderName}, discovering via CLI protocol...");
                    var discovered = await provider.DiscoverToolsAsync();
                    if (!discovered)
                    {
                        _logger.Log(LogLevel.Warn, $"Tool discovery failed for {provider.ProviderName}, provider will have no tools");
                    }
                }

                providers.Add(provider);
                _logger.Log(LogLevel.Info, $"Loaded provider: {provider.ProviderName}");
            }

            return providers;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, ex, $"Failed to load plugin configuration from {configPath}");
            return [];
        }
    }

    public async Task<bool> SaveConfigurationAsync(PluginConfiguration configuration, string configPath, CancellationToken cancellationToken = default)
    {
        try
        {
            await FileOperationHelper.WriteJsonAsync(
                configPath,
                configuration,
                McpHostContext.Default.PluginConfiguration,
                cancellationToken);
            _logger.Log(LogLevel.Info, $"Saved plugin configuration to {configPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, ex, $"Failed to save plugin configuration to {configPath}");
            return false;
        }
    }

    private CliToolProvider CreateProvider(CliProviderConfiguration config)
    {
        return new CliToolProvider(_logger, _packageManager, _processPoolManager, config);
    }
}
