global using static Common.Constants.ConstantManager;

using McpHost.Services;

namespace McpHost.Tools;

/// <summary>
/// MCP工具桥接器 - Host层面管理工具
/// 只暴露管理接口，CLI内部工具通过 tool_execute 间接调用
/// LLM 通过 tool_describe 按需渐进式获取插件命令详情
/// </summary>
internal sealed class CliBridgeTools : IDisposable
{
    private const int SummaryTriggerThreshold = 5;

    private readonly IToolRegistry _toolRegistry;
    private readonly IPackageManager _packageManager;
    private readonly ILogger _logger;
    private readonly ConversationTracker _tracker;
    private bool _disposed;

    public CliBridgeTools(
        IToolRegistry toolRegistry,
        IPackageManager packageManager,
        ILogger logger,
        ConversationTracker tracker)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _packageManager = packageManager ?? throw new ArgumentNullException(nameof(packageManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    /// <summary>
    /// 列出所有已注册插件（只显示插件级别信息，不暴露内部工具名）
    /// </summary>
    [McpTool("tool_list", @"
列出所有已注册的 CLI 工具插件概览。

【触发场景】
- 用户询问'有哪些插件'、'安装了什么工具'、'看看有什么功能'
- 用户想了解系统整体能力，需要全局视图
- 用户说'列出所有'、'显示全部'、'有什么可用'

【返回内容】
一次性返回所有插件的摘要列表（无分页）：名称、描述、分类、命令数量、是否有文档

【帮助系统】
- 每个插件的 HasDocumentation 字段标识是否提供内置帮助文档
- 可通过 tool_execute 调用 list_tools 命令获取插件内部命令详情
- 类似 git --no-pager，直接展开所有结果，不进入交互模式

【注意】
此接口只返回插件级别信息，不暴露内部工具名。如需查看具体命令，请使用 tool_describe 或执行 list_tools
")]
    public string ToolList()
    {
        var providers = _toolRegistry.GetProviderNames();
        var plugins = providers.Select(BuildPluginDescriptor).ToList();

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
    [McpTool("tool_describe", @"
获取指定插件的详细命令列表（渐进式发现，降低上下文成本）。

【触发场景】
- 用户已经通过 tool_search/tool_list 找到目标插件，想了解具体有哪些命令
- 用户询问'这个插件能做什么'、'XX插件有什么功能'、'查看memory的命令'
- 需要知道具体命令的参数格式才能调用 tool_execute

【返回内容】
一次性返回指定插件的所有命令详情（无分页）：命令名、描述、输入参数Schema（JSON Schema）

【帮助系统】
- 每个插件都内置 list_tools 帮助命令，可通过 tool_execute 调用
- 部分插件提供内置帮助文档（通过 HasDocumentation 标识）
- 命令参数遵循 JSON Schema 格式，可直接用于构建请求

【使用流程】
1. 先用 tool_list 或 tool_search 找到插件名
2. 再调用本接口或执行 list_tools 命令获取命令列表
3. 最后调用 tool_execute 执行具体命令

【设计理念】
- 类似 git --no-pager log，一次性展开所有结果
- 按需加载，避免一次性暴露所有工具造成上下文膨胀
")]
    public async Task<string> ToolDescribeAsync(
        [McpParameter("Plugin name to describe")] string pluginName)
    {
        if (string.IsNullOrWhiteSpace(pluginName))
            return CreateErrorResponse("Plugin name cannot be empty");

        var commands = await _toolRegistry.GetPluginCommandsAsync(pluginName);
        if (commands is null || commands.Count == 0)
            return CreateErrorResponse($"Plugin not found or no commands: {pluginName}");

        var metadata = _toolRegistry.GetProviderMetadata(pluginName);
        var result = new PluginDescribeResult
        {
            PluginName = pluginName,
            Description = metadata?.Description ?? $"{pluginName} CLI Plugin",
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
    [McpTool("tool_search", @"
搜索可用的 CLI 工具插件。当用户需要以下功能时调用此工具：

【触发场景】
- 用户提到'记忆'、'记住'、'忘记'、'回忆'、'知识图谱'、'实体'、'关系' 等词汇
- 用户询问'你能做什么'、'有什么工具'、'如何实现XX'
- 用户表达不确定或疑问，如'我不知道怎么弄'、'有没有工具可以...'
- 用户需要文件读取、数据存储、信息管理等通用能力

【返回内容】
返回匹配的插件列表（一次性全部返回，无分页），包含：名称、描述、分类、可用命令数量

【帮助命令】
找到插件后，可通过 tool_execute 执行 'list_tools' 命令查看该插件的完整命令列表和参数说明

【设计理念】
- 类似 git --no-pager，一次性输出全部结果，不进入交互模式
- 渐进式发现：先找插件 → 再看命令 → 最后执行，避免上下文膨胀

【使用流程】
1. 调用 tool_search 找到合适的插件
2. 调用 tool_execute 执行插件的 list_tools 命令查看详情
3. 调用 tool_execute 执行具体业务命令
")]
    public string ToolSearch(
        [McpParameter("Search keyword")] string query,
        [McpParameter("Maximum number of results")] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return CreateErrorResponse("Query cannot be empty");

        var providers = _toolRegistry.GetProviderNames();
        var matches = providers
            .Where(p =>
            {
                var meta = _toolRegistry.GetProviderMetadata(p);
                var desc = meta?.Description ?? $"{p} CLI Plugin";
                return p.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                       desc.Contains(query, StringComparison.OrdinalIgnoreCase);
            })
            .Take(limit)
            .Select(BuildPluginDescriptor)
            .ToList();

        if (matches.Count == 0)
            return CreateErrorResponse($"No plugins found matching '{query}'");

        var result = new ToolListResult
        {
            TotalPlugins = matches.Count,
            Plugins = matches
        };

        return JsonSerializer.Serialize(result, CommonJsonContext.Default.ToolListResult);
    }

    /// <summary>
    /// 执行CLI工具（唯一能调用内部工具的入口）
    /// </summary>
    [McpTool("tool_execute", @"
执行 CLI 工具命令（唯一能调用内部工具的入口）。

【触发场景】
- 用户明确要求执行某个具体操作，如'创建一个实体'、'读取文件'、'搜索记忆'
- 已经通过 tool_describe 或 list_tools 获取到命令详情，现在需要实际执行
- 用户说'帮我做XX'、'执行XX命令'、'调用XX功能'
- 需要调用帮助命令：list_tools（查看插件命令列表）

【参数说明】
- tool: 要执行的命令名（如 memory_create_entities, file_reader_read_head, list_tools）
- parameters: 命令参数，必须是 JSON 对象格式

【返回内容】
一次性返回完整执行结果（无分页/无交互），JSON格式包含 success/data/message 等字段

【内置帮助命令】
每个 CLI 插件都支持 list_tools 命令，用于：
- 查看该插件的所有可用命令列表
- 获取命令描述和参数说明
- 了解插件能力边界

示例：调用命令参数 {""command"": ""list_tools""} 获取 MemoryCli 的完整命令文档

【前置条件】
建议先通过 tool_search → tool_describe/list_tools 流程找到正确的命令名和参数格式

【重要提示】
1. 这是唯一能调用 CLI 内部工具的接口
2. 不要尝试直接调用 memory_xxx 等内部命令名，必须通过此接口间接调用
3. 类似 git --no-pager，所有结果一次性返回，无需翻页
")]
    public async Task<string> ToolExecuteAsync(
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

            var description = ExtractDescription(tool, parameters);
            _tracker.Record(tool, description);

            if (_tracker.TotalCount > 0 && _tracker.TotalCount % SummaryTriggerThreshold == 0)
            {
                _ = TriggerAutoSummaryAsync();
            }

            return result.Output ?? "{\"success\":false,\"message\":\"No output from CLI tool\",\"data\":null}";
        }
        catch (Exception ex)
        {
            await _logger.LogAsync(LogLevel.Error, ex, $"Tool execution failed: {tool}");
            return CreateErrorResponse($"Tool execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取提供者信息
    /// </summary>
    [McpTool("provider_list", @"
列出所有已注册的工具提供者（插件提供方信息）。

【触发场景】
- 用户询问'有哪些工具提供者'、'谁提供了这些工具'
- 需要了解插件来源或进行调试诊断
- 系统管理员查看已加载的 CLI 插件列表

【返回内容】
返回提供者名称和每个提供者下的工具数量

【与 tool_list 的区别】
- tool_list: 返回插件的业务信息（描述、分类）
- provider_list: 返回技术层面的提供者注册信息
")]
    public string ProviderList()
    {
        var providers = _toolRegistry.GetProviderNames();

        var providerInfo = providers.Select(p =>
        {
            var meta = _toolRegistry.GetProviderMetadata(p);
            return new ProviderInfo
            {
                Name = p,
                ToolCount = meta?.CommandCount ?? 0
            };
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
    [McpTool("package_status", @"
查询包（插件）的安装状态。

【触发场景】
- 用户询问'XX插件安装了吗'、'检查安装状态'、'是否已安装'
- 在执行工具前确认依赖是否就绪
- 用户说'看看装了没有'、'检查一下环境'

【参数说明】
- packageName: 可选，指定包名查询特定包；不传则返回全局状态

【返回内容】
返回安装状态、包名称、工具目录路径等信息
")]
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
    [McpTool("package_install", @"
安装新的工具包（插件）。

【触发场景】
- 用户要求'安装XX插件'、'下载XX工具'、'添加新功能'
- tool_search 找不到需要的工具，需要先安装对应包
- 用户说'我需要XX功能'但系统尚未安装该插件

【参数说明】
- packageName: 要安装的包名称

【前置建议】
可先用 package_status 检查是否已安装，避免重复安装

【返回内容】
返回安装结果：成功/失败状态、包名、消息
")]
    public async Task<string> PackageInstallAsync(
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
            await _logger.LogAsync(LogLevel.Error, ex, $"Package installation failed: {packageName}");
            return CreateErrorResponse($"Failed to install package {packageName}: {ex.Message}");
        }
    }

    private PluginDescriptor BuildPluginDescriptor(string providerName)
    {
        var meta = _toolRegistry.GetProviderMetadata(providerName);
        return new PluginDescriptor
        {
            Name = providerName,
            Description = meta?.Description ?? $"{providerName} CLI Plugin",
            Category = meta?.Category ?? "general",
            CommandCount = meta?.CommandCount ?? 0,
            HasDocumentation = meta?.HasDocumentation ?? true
        };
    }

    private static string ExtractDescription(string toolName, Dictionary<string, JsonElement> parameters)
    {
        return toolName switch
        {
            string t when t.StartsWith("memory_create_entities") || t.StartsWith("memory_save_summary") =>
                TryGetStringArray(parameters, "entities") is { Count: > 0 } names
                    ? $"{t}: {string.Join(", ", names.Take(3))}"
                    : t,
            string t when t.StartsWith("memory_search_nodes") =>
                TryGetString(parameters, "query") is { Length: > 0 } q
                    ? $"{t}: search '{q}'"
                    : t,
            _ => toolName
        };
    }

    private static string? TryGetString(Dictionary<string, JsonElement> parameters, string key)
    {
        return parameters.TryGetValue(key, out var elem) && elem.ValueKind == JsonValueKind.String
            ? elem.GetString()
            : null;
    }

    private static List<string>? TryGetStringArray(Dictionary<string, JsonElement> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var elem) || elem.ValueKind != JsonValueKind.Array)
            return null;

        var names = new List<string>();
        foreach (var item in elem.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("name", out var nameProp) &&
                nameProp.ValueKind == JsonValueKind.String)
            {
                names.Add(nameProp.GetString() ?? "");
            }
        }
        return names;
    }

    private async Task TriggerAutoSummaryAsync()
    {
        try
        {
            var recentCalls = _tracker.GetRecent(SummaryTriggerThreshold);
            if (recentCalls.Count == 0) return;

            var userMessages = recentCalls
                .Select(c => string.IsNullOrEmpty(c.Description) ? c.ToolName : c.Description)
                .ToList();

            var title = $"Session activity ({recentCalls[0].Timestamp:yyyy-MM-dd HH:mm})";

            var request = new CliRequest
            {
                Command = "save_summary",
                Title = title,
                UserMessages = userMessages
            };

            var jsonParams = JsonSerializer.Serialize(request, CommonJsonContext.Default.CliRequest);
            using var doc = JsonDocument.Parse(jsonParams);
            var parameters = new Dictionary<string, JsonElement>();
            foreach (var prop in doc.RootElement.EnumerateObject())
                parameters[prop.Name] = prop.Value.Clone();

            await _toolRegistry.ExecuteToolAsync("memory_save_summary", parameters);
            await _logger.InfoAsync($"Auto-saved conversation summary: {title}");
        }
        catch (Exception ex)
        {
            await _logger.LogAsync(LogLevel.Warn, ex, "Failed to auto-save conversation summary");
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
