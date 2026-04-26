global using static Common.Constants.ConstantManager;

namespace McpHost.Tools;

/// <summary>
/// MCP工具桥接器 - 使用插件架构
/// 通过IToolRegistry执行工具，支持动态插件注册
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
    /// 搜索可用工具
    /// </summary>
    [McpTool("tool_search", "Search available CLI tools by keyword")]
    public string ToolSearch(
        [McpParameter("Search keyword")] string query,
        [McpParameter("Maximum number of results")] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return CreateErrorResponse("Query cannot be empty");

        var allTools = _toolRegistry.GetAllTools();
        var matches = allTools
            .Where(t => t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        t.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(t => new ToolSearchResult
            {
                Name = t.Name,
                Description = t.Description,
                Category = t.Category
            })
            .ToList();

        if (matches.Count == 0)
            return CreateErrorResponse($"No tools found matching '{query}'");

        return JsonSerializer.Serialize(matches, CommonJsonContext.Default.ListToolSearchResult);
    }

    /// <summary>
    /// 获取工具详细信息
    /// </summary>
    [McpTool("tool_get", "Get detailed information about a specific tool")]
    public string ToolGet(
        [McpParameter("Tool name")] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return CreateErrorResponse("Tool name cannot be empty");

        if (!_toolRegistry.TryGetTool(name, out var metadata) || metadata is null)
            return CreateErrorResponse($"Tool not found: {name}");

        var toolInfo = new ToolInfoResult
        {
            Name = metadata.Name,
            Description = metadata.Description,
            Category = metadata.Category,
            InputSchema = metadata.InputSchema
        };

        return JsonSerializer.Serialize(toolInfo, CommonJsonContext.Default.ToolInfoResult);
    }

    /// <summary>
    /// 执行工具
    /// </summary>
    [McpTool("tool_execute", "Execute a CLI tool with JSON parameters")]
    public async Task<string> ToolExecute(
        [McpParameter("Tool name")] string tool,
        [McpParameter("Tool parameters as JSON object")] Dictionary<string, JsonElement> parameters)
    {
        if (string.IsNullOrWhiteSpace(tool))
            return CreateErrorResponse("Tool name cannot be empty");

        if (parameters is null || parameters.Count == 0)
            return CreateErrorResponse("Parameters cannot be empty");

        try
        {
            var result = await _toolRegistry.ExecuteToolAsync(tool, parameters);

            // CLI 工具返回的已经是 OperationResult<JsonElement> 格式的 JSON，直接返回
            return result.Output ?? "{\"success\":false,\"message\":\"No output from CLI tool\",\"data\":null}";
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, ex, $"Tool execution failed: {tool}");
            return CreateErrorResponse($"Tool execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取所有已注册工具列表
    /// </summary>
    [McpTool("tool_list", "List all registered tools")]
    public string ToolList()
    {
        var tools = _toolRegistry.GetAllTools();
        var providers = _toolRegistry.GetProviderNames();

        var result = new ToolListResult
        {
            TotalTools = tools.Count,
            Providers = providers,
            Tools = tools.Select(t => new ToolSummary
            {
                Name = t.Name,
                Description = t.Description,
                Category = t.Category
            }).ToList()
        };

        return JsonSerializer.Serialize(result, CommonJsonContext.Default.ToolListResult);
    }

    /// <summary>
    /// 获取提供者信息
    /// </summary>
    [McpTool("provider_list", "List all registered tool providers")]
    public string ProviderList()
    {
        var providers = _toolRegistry.GetProviderNames();
        var tools = _toolRegistry.GetAllTools();

        var providerInfo = providers.Select(p => new ProviderInfo
        {
            Name = p,
            ToolCount = tools.Count(t => true)
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
