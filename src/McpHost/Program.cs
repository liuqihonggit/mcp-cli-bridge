#pragma warning disable IL2026
#pragma warning disable IL3050

using System.Diagnostics;

/// <summary>
/// 自动发现 CLI 插件 - 查找同目录下的所有 .exe 文件
/// </summary>
static List<string> DiscoverCliPlugins(IPackageManager packageManager, ILogger logger)
{
    return packageManager.DiscoverAvailablePlugins().ToList();
}

var services = new ServiceContainer();

services.AddInstance<ILogger>(new Logger(
    LogOutput.StdErr,
    LogLevel.Info,
    nameof(McpHost)));

services.AddSingleton<ICacheProvider>(sp =>
{
    var logger = sp.GetService<ILogger>();
    return new MemoryCacheProvider(logger, MemoryCacheOptions.Default);
});

services.AddSingleton<IInputValidator, Common.Security.Validation.JsonSchemaValidator>();
services.AddSingleton<IPermissionChecker, Common.Security.Permissions.WhitelistPermissionChecker>();
services.AddSingleton<ISecurityValidator>(sp =>
{
    var inputValidator = sp.GetService<IInputValidator>();
    var permissionChecker = sp.GetService<IPermissionChecker>();
    return new SecurityValidator(inputValidator, permissionChecker);
});

services.AddSingleton<IProcessPoolManager, ProcessPoolManager>();
services.AddSingleton<IPackageManager, PackageManager>();

services.AddSingleton<IToolRegistry>(sp =>
{
    var logger = sp.GetService<ILogger>();
    var cache = sp.GetService<ICacheProvider>();
    return new ToolRegistry(logger, cache);
});

services.AddSingleton<IMiddlewarePipeline>(sp =>
{
    var pipeline = new MiddlewarePipeline(sp);
    pipeline.Use<ExceptionHandlingMiddleware>();
    pipeline.Use<LoggingMiddleware>();
    pipeline.Use<Common.Security.Middleware.SecurityValidationMiddleware>();
    pipeline.Use<Common.Caching.CacheMiddleware>();
    return pipeline;
});

services.AddSingleton<CliBridgeTools>();

var serviceProvider = services;
var logger = serviceProvider.GetService<ILogger>();

if (args.Length > 0 && (args[0] == Commands.Cli.Help || args[0] == Commands.Cli.HelpShort || args[0] == Commands.Cli.HelpWindows))
{
    logger.Info("McpHost - MCP CLI Bridge Server with Plugin Architecture");
    logger.Info("");
    logger.Info("Usage: McpHost");
    logger.Info("");
    logger.Info("This is an MCP server that runs in stdio mode.");
    logger.Info("It communicates via JSON-RPC over stdin/stdout.");
    logger.Info("");
    logger.Info("Features:");
    logger.Info("  - Dynamic tool discovery via CLI protocol (list_tools)");
    logger.Info("  - Plugin-based tool registration");
    logger.Info("  - Process pool for CLI execution");
    logger.Info("  - Memory cache for metadata and results");
    logger.Info("  - Security validation and permission checking");
    logger.Info("");
    logger.Info("Options:");
    logger.Info("  -h, --help, /?    Show this help message");
    return;
}

try
{
    logger.Info("Starting McpHost server with plugin architecture...");

    var toolRegistry = serviceProvider.GetService<IToolRegistry>();
    var packageManager = serviceProvider.GetService<IPackageManager>();

    // 自动发现同目录下的所有 CLI.exe 插件
    var discoveredPlugins = DiscoverCliPlugins(packageManager, logger);

    foreach (var pluginName in discoveredPlugins)
    {
        var config = DefaultPluginConfiguration.CreateCliProvider(
            pluginName,
            pluginName,
            processPoolSize: 5,
            timeout: TimeSpan.FromSeconds(30));

        var provider = new CliToolProvider(
            serviceProvider.GetService<ILogger>(),
            packageManager,
            serviceProvider.GetService<IProcessPoolManager>(),
            config);

        var discovered = await provider.DiscoverToolsAsync();
        if (discovered)
        {
            toolRegistry.RegisterProvider(provider);
            logger.Info($"Registered plugin: {pluginName}");
        }
        else
        {
            logger.Warn($"Failed to discover tools for provider: {pluginName}");
            provider.Dispose();
        }
    }

    logger.Info($"Total plugins registered: {toolRegistry.GetProviderNames().Count}");

    var server = new McpServer(nameof(McpHost), Versions.McpHost);

    // 只注册 Host 层面的管理工具，不暴露 CLI 内部工具
    var bridgeTools = serviceProvider.GetService<CliBridgeTools>();
    if (bridgeTools is not null)
    {
        var adapter = new McpToolAdapter();
        adapter.RegisterTool(bridgeTools);

        foreach (var handler in adapter.GetHandlers())
        {
            server.RegisterToolHandler(handler);
        }
    }

    logger.Info("Server started, waiting for requests...");
    await server.RunAsync();
}
catch (Exception ex)
{
    logger.Error(ex, "Server error");
}
finally
{
    var processPoolManager = serviceProvider.GetService<IProcessPoolManager>();
    if (processPoolManager is IAsyncDisposable asyncDisposable)
    {
        await asyncDisposable.DisposeAsync();
    }

    logger.Info("Server stopped");
}
