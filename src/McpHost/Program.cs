#pragma warning disable IL2026
#pragma warning disable IL3050

using System.Diagnostics;

/// <summary>
/// 自动发现 CLI 插件 - 查找同目录下的所有 .exe 文件，名称规范化为小写下划线格式
/// </summary>
static List<(string NormalizedName, string ExeFileName)> DiscoverCliPlugins(IPackageManager packageManager, ILogger logger)
{
    return packageManager.DiscoverAvailablePlugins()
        .Select(exeName => (NormalizedName: CliNaming.ToSnakeCase(exeName), ExeFileName: exeName))
        .ToList();
}

static string BuildInstructions()
{
    return """
记忆系统使用指南：
1. 新对话开始时，调用 tool_execute(tool="memory_get_recent_summaries", parameters={"command":"get_recent_summaries","limit":5}) 获取近期对话摘要，了解用户最近关注的内容
2. 当用户让你"记住"某个事实时，调用 tool_execute(tool="memory_create_entities", parameters={"command":"create_entities","entities":[...]}) 保存（同名实体会覆盖更新）
3. 需要回忆用户信息时，调用 tool_execute(tool="memory_search_nodes", parameters={"command":"search_nodes","query":"..."}) 搜索
4. 系统会自动保存对话摘要，你也可以主动调用 tool_execute(tool="memory_save_summary", parameters={"command":"save_summary","title":"...","userMessages":[...]}) 保存重要对话
5. 调用 tool_search("记忆") 可发现记忆相关插件，tool_describe("memory_cli") 查看全部命令
""";
}

var services = new ServiceContainer();

services.AddInstance<ILogger>(new Logger(
    LogOutput.StdErr,
    LogLevel.Info,
    nameof(McpHost)));

services.AddSingleton<ICacheProvider>(sp =>
{
    var logger = sp.GetService<ILogger>();
    return CacheProviderFactory.Create(logger!);
});

services.AddSingleton<IProcessPoolManager>(sp =>
{
    var logger = sp.GetService<ILogger>();
    return new ProcessPoolManager(logger, healthCheckInterval: null);
});
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
    pipeline.Use<Common.Caching.CacheMiddleware>();
    return pipeline;
});

services.AddSingleton<CliBridgeTools>();
services.AddSingleton<ConversationTracker>();

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

    foreach (var (normalizedName, exeFileName) in discoveredPlugins)
    {
        var config = DefaultPluginConfiguration.CreateCliProvider(
            normalizedName,
            exeFileName,
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
            logger.Info($"Registered plugin: {normalizedName}");
        }
        else
        {
            logger.Warn($"Failed to discover tools for provider: {normalizedName}");
            provider.Dispose();
        }
    }

    logger.Info($"Total plugins registered: {toolRegistry.GetProviderNames().Count}");

    CommandTable.PrintTable(line => logger.Info(line));

    var server = new McpServer(nameof(McpHost), Versions.McpHost, instructions: BuildInstructions());

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
