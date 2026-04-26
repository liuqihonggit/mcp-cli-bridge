global using static Common.Constants.ConstantManager;

namespace McpHost.Tools;

/// <summary>
/// MCP工具桥接器 - Host层面管理工具
/// 只暴露管理接口，CLI内部工具通过 tool_execute 间接调用
/// LLM 通过 tool_describe 按需渐进式获取插件命令详情
/// </summary>
internal sealed class CliBridgeTools : IDisposable
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IPackageManager _packageManager;
    private readonly ILogger _logger;
    private bool _disposed;

    public CliBridgeTools(
        IToolRegistry toolRegistry,
        IPackageManager packageManager,
        ILogger logger)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _packageManager = packageManager ?? throw new ArgumentNullException(nameof(packageManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 列出所有已注册插件（只显示插件级别信息，不暴露内部工具名）
    /// </summary>
    [McpTool("tool_list", "List all registered plugins with summary info")]
    public string ToolList()
    {
        var providers = _toolRegistry.GetProviderNames();
        var plugins = providers.Select(p => new PluginDescriptor
        {
            Name = p,
            Description = GetPluginDescription(p),
            Category = GetPluginCategory(p),
            CommandCount = GetPluginCommandCount(p),
            HasDocumentation = true
        }).ToList();

        var result = new ToolListResult
        {
            TotalPlugins = plugins.Count,
            Plugins = plugins
        };

        return JsonSerializer.Serialize(result, CommonJsonContext.Default.ToolListResult);
    }

    /// <summary>
    /// 按需获取插件的详细命令列表（渐进式发现，降低上下文成本）
    /// </summary>
    [McpTool("tool_describe", "Get detailed command list for a specific plugin (progressive discovery)")]
    public async Task<string> ToolDescribe(
        [McpParameter("Plugin name to describe")] string pluginName)
    {
        if (string.IsNullOrWhiteSpace(pluginName))
            return CreateErrorResponse("Plugin name cannot be empty");

        var commands = await _toolRegistry.GetPluginCommandsAsync(pluginName);
        if (commands is null || commands.Count == 0)
            return CreateErrorResponse($"Plugin not found or no commands: {pluginName}");

        var result = new PluginDescribeResult
        {
            PluginName = pluginName,
            Description = GetPluginDescription(pluginName),
            Commands = commands.Select(c => new CommandDescriptor
            {
                Name = c.Name,
                Description = c.Description,
                InputSchema = c.InputSchema
            }).ToList()
        };

        return JsonSerializer.Serialize(result, CommonJsonContext.Default.PluginDescribeResult);
    }

    /// <summary>
    /// 搜索可用插件（搜索插件名和描述，不搜索内部工具名）
    /// </summary>
    [McpTool("tool_search", "Search available plugins by keyword")]
    public string ToolSearch(
        [McpParameter("Search keyword")] string query,
        [McpParameter("Maximum number of results")] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return CreateErrorResponse("Query cannot be empty");

        var providers = _toolRegistry.GetProviderNames();
        var matches = providers
            .Where(p => p.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        GetPluginDescription(p).Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(p => new PluginDescriptor
            {
                Name = p,
                Description = GetPluginDescription(p),
                Category = GetPluginCategory(p),
                CommandCount = GetPluginCommandCount(p),
                HasDocumentation = true
            })
            .ToList();

        if (matches.Count == 0)
            return CreateErrorResponse($"No plugins found matching '{query}'");

        return JsonSerializer.Serialize(matches, CommonJsonContext.Default.ListPluginDescriptor);
    }

    /// <summary>
    /// 执行CLI工具（唯一能调用内部工具的入口）
    /// </summary>
    [McpTool("tool_execute", "Execute a CLI tool command with JSON parameters")]
    public async Task<string> ToolExecute(
        [McpParameter("Tool/Command name")] string tool,
        [McpParameter("Tool parameters as JSON object")] Dictionary<string, JsonElement> parameters)
    {
        if (string.IsNullOrWhiteSpace(tool))
            return CreateErrorResponse("Tool name cannot be empty");

        if (parameters is null || parameters.Count == 0)
            return CreateErrorResponse("Parameters cannot be empty");

        try
        {
            var result = await _toolRegistry.ExecuteToolAsync(tool, parameters);
            return result.Output ?? "{\"success\":false,\"message\":\"No output from CLI tool\",\"data\":null}";
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, ex, $"Tool execution failed: {tool}");
            return CreateErrorResponse($"Tool execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取提供者信息
    /// </summary>
    [McpTool("provider_list", "List all registered tool providers")]
    public string ProviderList()
    {
        var providers = _toolRegistry.GetProviderNames();

        var providerInfo = providers.Select(p => new ProviderInfo
        {
            Name = p,
            ToolCount = GetPluginCommandCount(p)
        }).ToList();

        var result = new ProviderListResult
        {
            TotalProviders = providers.Count,
            Providers = providerInfo
        };

        return JsonSerializer.Serialize(result, CommonJsonContext.Default.ProviderListResult);
    }

    /// <summary>
    /// 获取包安装状态
    /// </summary>
    [McpTool("package_status", "Get package installation status")]
    public string PackageStatus(
        [McpParameter("Package name")] string? packageName = null)
    {
        var result = new PackageStatusResult
        {
            IsInstalled = string.IsNullOrEmpty(packageName) ? true : _packageManager.GetExecutablePath(packageName) != null,
            PackageName = packageName ?? "all",
            ToolsDirectory = _packageManager.GetToolsDirectory()
        };

        return JsonSerializer.Serialize(result, CommonJsonContext.Default.PackageStatusResult);
    }

    /// <summary>
    /// 安装包
    /// </summary>
    [McpTool("package_install", "Install a package")]
    public async Task<string> PackageInstall(
        [McpParameter("Package name to install")] string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            return CreateErrorResponse("Package name cannot be empty");

        try
        {
            var success = await _packageManager.DownloadPackageAsync(packageName);

            var result = new PackageInstallResult
            {
                Success = success,
                PackageName = packageName,
                Message = success ? $"Package {packageName} installed successfully" : $"Failed to install package {packageName}"
            };

            return JsonSerializer.Serialize(result, CommonJsonContext.Default.PackageInstallResult);
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, ex, $"Package installation failed: {packageName}");
            return CreateErrorResponse($"Failed to install package {packageName}: {ex.Message}");
        }
    }

    private static string GetPluginDescription(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "memory" or "memorycli" or "jj_memory" => "Knowledge Graph CLI - Manage entities, relations, and observations",
            "file_reader" or "filereadercli" or "file_reader_cli" => "File Reader CLI - Read file contents with line control",
            _ => $"{providerName} CLI Plugin"
        };
    }

    private static string GetPluginCategory(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "memory" or "memorycli" or "jj_memory" => "knowledge-graph",
            "file_reader" or "filereadercli" or "file_reader_cli" => "file-operations",
            _ => "general"
        };
    }

    private int GetPluginCommandCount(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "memory" or "memorycli" or "jj_memory" => 7,
            "file_reader" or "filereadercli" or "file_reader_cli" => 2,
            _ => 0
        };
    }

    private static string CreateErrorResponse(string message)
    {
        var error = new ErrorResponse { Error = message };
        return JsonSerializer.Serialize(error, CommonJsonContext.Default.ErrorResponse);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }
}
