namespace Common.Constants;

/// <summary>
/// 常量分类特性 - 用于标记常量的分类
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class ConstantCategoryAttribute : Attribute
{
    public string Category { get; }
    public string? SubCategory { get; }
    public string? Description { get; }

    public ConstantCategoryAttribute(string category, string? subCategory = null, string? description = null)
    {
        Category = category;
        SubCategory = subCategory;
        Description = description;
    }
}

/// <summary>
/// 常量元数据
/// </summary>
public sealed class ConstantMetadata
{
    public required string Name { get; init; }
    public required string Category { get; init; }
    public string? SubCategory { get; init; }
    public string? Description { get; init; }
    public required object Value { get; init; }
    public Type ValueType { get; init; } = typeof(string);
}

/// <summary>
/// 统一常量管理器 - 集中管理所有应用程序常量
/// </summary>
public static class ConstantManager
{
    private static readonly Dictionary<string, ConstantMetadata> _constants = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Dictionary<string, List<ConstantMetadata>>> _categoryIndex = new(StringComparer.Ordinal);

    static ConstantManager()
    {
        InitializeConstants();
    }

    /// <summary>
    /// 初始化所有常量
    /// </summary>
    private static void InitializeConstants()
    {
        // 文件扩展名
        Register(nameof(FileExtensions.Jsonl), Categories.File, SubCategories.Extension, ".jsonl");
        Register(nameof(FileExtensions.Json), Categories.File, SubCategories.Extension, ".json");
        Register(nameof(FileExtensions.Exe), Categories.File, SubCategories.Extension, ".exe");
        Register(nameof(FileExtensions.Tgz), Categories.File, SubCategories.Extension, ".tgz");

        // 文件名
        Register(nameof(FileNames.Memory), Categories.File, SubCategories.Name, "memory");
        Register(nameof(FileNames.Relations), Categories.File, SubCategories.Name, "memory_relations");
        Register(nameof(FileNames.Tools), Categories.File, SubCategories.Name, "tools");
        Register(nameof(FileNames.Npm), Categories.File, SubCategories.Name, "npm");
        Register(nameof(FileNames.NpmCmd), Categories.File, SubCategories.Name, "npm.cmd");

        // 目录名
        Register(nameof(DirectoryNames.Cache), Categories.Directory, null, "Cache");
        Register(nameof(DirectoryNames.Tools), Categories.Directory, null, "tools");
        Register(nameof(DirectoryNames.Cli), Categories.Directory, null, "cli");
        Register(nameof(DirectoryNames.Logs), Categories.Directory, null, "logs");
        Register(nameof(DirectoryNames.Plugins), Categories.Directory, null, "Plugins");

        // 超时设置
        RegisterTimeSpan(nameof(Timeouts.DefaultCommand), Categories.Timeout, null, TimeSpan.FromSeconds(30));
        RegisterTimeSpan(nameof(Timeouts.DefaultLock), Categories.Timeout, null, TimeSpan.FromSeconds(5));
        RegisterTimeSpan(nameof(Timeouts.ProcessExitDelay), Categories.Timeout, null, TimeSpan.FromMilliseconds(10));

        // 重试策略
        RegisterInt(nameof(RetryPolicy.MaxRetries), Categories.Retry, null, 3);
        RegisterInt(nameof(RetryPolicy.BaseDelayMs), Categories.Retry, null, 100);

        // 分隔符
        RegisterChar(nameof(Separators.RelationKey), Categories.Separator, null, '|');
        Register(nameof(Separators.PackagePrefix), Categories.Separator, null, "package/");

        // 平台标识
        Register(nameof(Platforms.Windows), Categories.Platform, null, "win");
        Register(nameof(Platforms.Linux), Categories.Platform, null, "linux");
        Register(nameof(Platforms.OSX), Categories.Platform, null, "osx");
        Register(nameof(Platforms.Unknown), Categories.Platform, null, "unknown");

        // JSON-RPC
        Register(nameof(JsonRpc.ContentLengthPrefix), Categories.JsonRpc, null, "Content-Length:");
        Register(nameof(JsonRpc.ProtocolVersion), Categories.JsonRpc, null, "2024-11-05");

        // 错误码
        RegisterInt(nameof(ErrorCodes.MethodNotFound), Categories.ErrorCode, null, -32601);
        RegisterInt(nameof(ErrorCodes.InternalError), Categories.ErrorCode, null, -32603);

        // 命名后缀
        Register(nameof(Suffixes.Service), Categories.Suffix, null, "Service");
        Register(nameof(Suffixes.Provider), Categories.Suffix, null, "Provider");
        Register(nameof(Suffixes.Repository), Categories.Suffix, null, "Repository");

        // 日期时间格式
        Register(nameof(DateTimeFormats.Timestamp), Categories.DateTimeFormat, null, "O");
        Register(nameof(DateTimeFormats.FileTimestamp), Categories.DateTimeFormat, null, "yyyyMMdd_HHmmss");
        Register(nameof(DateTimeFormats.LogFile), Categories.DateTimeFormat, null, "yyyy-MM-dd");
        Register(nameof(DateTimeFormats.LogEntry), Categories.DateTimeFormat, null, "yyyy-MM-dd HH:mm:ss.fff");

        // JSON值类型
        Register(nameof(JsonValueTypes.ObjectType), Categories.JsonValueType, null, "object");
        Register(nameof(JsonValueTypes.StringType), Categories.JsonValueType, null, "string");
        Register(nameof(JsonValueTypes.IntegerType), Categories.JsonValueType, null, "integer");
        Register(nameof(JsonValueTypes.Number), Categories.JsonValueType, null, "number");
        Register(nameof(JsonValueTypes.Boolean), Categories.JsonValueType, null, "boolean");
        Register(nameof(JsonValueTypes.Array), Categories.JsonValueType, null, "array");

        // 内容类型
        Register(nameof(ContentTypes.Text), Categories.ContentType, null, "text");

        // 版本号
        Register(nameof(Versions.McpHost), Categories.Version, null, "1.0.0");

        // 项目路径
        Register(nameof(ProjectPaths.BinDirectory), Categories.ProjectPath, null, "bin");
        Register(nameof(ProjectPaths.ReleaseConfiguration), Categories.ProjectPath, null, "Release");
        Register(nameof(ProjectPaths.DebugConfiguration), Categories.ProjectPath, null, "Debug");
        Register(nameof(ProjectPaths.TargetFramework), Categories.ProjectPath, null, "net10.0");
        Register(nameof(ProjectPaths.WindowsX64Runtime), Categories.ProjectPath, null, "win-x64");
        Register(nameof(ProjectPaths.PublishDirectory), Categories.ProjectPath, null, "publish");

        // 命令名称 - Memory
        Register(nameof(Commands.Memory.CreateEntities), Categories.Command, SubCategories.MemoryCommand, "create_entities");
        Register(nameof(Commands.Memory.CreateRelations), Categories.Command, SubCategories.MemoryCommand, "create_relations");
        Register(nameof(Commands.Memory.ReadGraph), Categories.Command, SubCategories.MemoryCommand, "read_graph");
        Register(nameof(Commands.Memory.SearchNodes), Categories.Command, SubCategories.MemoryCommand, "search_nodes");
        Register(nameof(Commands.Memory.AddObservations), Categories.Command, SubCategories.MemoryCommand, "add_observations");
        Register(nameof(Commands.Memory.DeleteEntities), Categories.Command, SubCategories.MemoryCommand, "delete_entities");
        Register(nameof(Commands.Memory.DeleteObservations), Categories.Command, SubCategories.MemoryCommand, "delete_observations");
        Register(nameof(Commands.Memory.DeleteRelations), Categories.Command, SubCategories.MemoryCommand, "delete_relations");
        Register(nameof(Commands.Memory.OpenNodes), Categories.Command, SubCategories.MemoryCommand, "open_nodes");
        Register(nameof(Commands.Memory.GetStorageInfo), Categories.Command, SubCategories.MemoryCommand, "get_storage_info");
        Register(nameof(Commands.Memory.ListTools), Categories.Command, SubCategories.MemoryCommand, "list_tools");

        // 命令名称 - MCP
        Register(nameof(Commands.Mcp.Initialize), Categories.Command, SubCategories.McpCommand, "initialize");
        Register(nameof(Commands.Mcp.ToolsList), Categories.Command, SubCategories.McpCommand, "tools/list");
        Register(nameof(Commands.Mcp.ToolsCall), Categories.Command, SubCategories.McpCommand, "tools/call");
        Register(nameof(Commands.Mcp.Initialized), Categories.Command, SubCategories.McpCommand, "initialized");

        // 命令名称 - CLI
        Register(nameof(Commands.Cli.Help), Categories.Command, SubCategories.CliCommand, "--help");
        Register(nameof(Commands.Cli.HelpShort), Categories.Command, SubCategories.CliCommand, "-h");
        Register(nameof(Commands.Cli.HelpWindows), Categories.Command, SubCategories.CliCommand, "/?");
        Register(nameof(Commands.Cli.JsonInput), Categories.Command, SubCategories.CliCommand, "--json-input");
        Register(nameof(Commands.Cli.Command), Categories.Command, SubCategories.CliCommand, "--command");

        // 命令名称 - FileReader
        Register(nameof(Commands.FileReader.ReadHead), Categories.Command, SubCategories.FileReaderCommand, "read_head");
        Register(nameof(Commands.FileReader.ReadTail), Categories.Command, SubCategories.FileReaderCommand, "read_tail");
        Register(nameof(Commands.FileReader.ListTools), Categories.Command, SubCategories.FileReaderCommand, "list_tools");

        // 日志级别
        Register(nameof(LogLevels.Debug), Categories.LogLevel, null, "DBG");
        Register(nameof(LogLevels.Info), Categories.LogLevel, null, "INF");
        Register(nameof(LogLevels.Warn), Categories.LogLevel, null, "WRN");
        Register(nameof(LogLevels.Error), Categories.LogLevel, null, "ERR");
        Register(nameof(LogLevels.Unknown), Categories.LogLevel, null, "???");

        // 互斥锁名称
        Register(nameof(MutexNames.McpHostSingleInstance), Categories.MutexName, null, "Global\\McpHost_SingleInstance");

        // 安全事件类型
        Register(nameof(Security.EventTypes.InputValidationFailed), Categories.SecurityEvent, null, "INPUT_VALIDATION_FAILED");
        Register(nameof(Security.EventTypes.PermissionDenied), Categories.SecurityEvent, null, "PERMISSION_DENIED");
        Register(nameof(Security.EventTypes.MaliciousContentDetected), Categories.SecurityEvent, null, "MALICIOUS_CONTENT_DETECTED");
        Register(nameof(Security.EventTypes.ToolExecutionBlocked), Categories.SecurityEvent, null, "TOOL_EXECUTION_BLOCKED");
        Register(nameof(Security.EventTypes.WhitelistViolation), Categories.SecurityEvent, null, "WHITELIST_VIOLATION");
        Register(nameof(Security.EventTypes.SchemaValidationFailed), Categories.SecurityEvent, null, "SCHEMA_VALIDATION_FAILED");
        Register(nameof(Security.EventTypes.UnauthorizedAccess), Categories.SecurityEvent, null, "UNAUTHORIZED_ACCESS");

        // 攻击类型
        Register(nameof(Security.AttackTypes.SqlInjection), Categories.AttackType, null, "SQL_INJECTION");
        Register(nameof(Security.AttackTypes.CommandInjection), Categories.AttackType, null, "COMMAND_INJECTION");
        Register(nameof(Security.AttackTypes.Xss), Categories.AttackType, null, "XSS");
        Register(nameof(Security.AttackTypes.PathTraversal), Categories.AttackType, null, "PATH_TRAVERSAL");
        Register(nameof(Security.AttackTypes.JsonInjection), Categories.AttackType, null, "JSON_INJECTION");
        Register(nameof(Security.AttackTypes.ScriptInjection), Categories.AttackType, null, "SCRIPT_INJECTION");

        // 权限级别
        Register(nameof(Security.PermissionLevels.Admin), Categories.PermissionLevel, null, "admin");
        Register(nameof(Security.PermissionLevels.PowerUser), Categories.PermissionLevel, null, "power_user");
        Register(nameof(Security.PermissionLevels.User), Categories.PermissionLevel, null, "user");
        Register(nameof(Security.PermissionLevels.Guest), Categories.PermissionLevel, null, "guest");

        // 角色权限
        Register(nameof(Security.RolePermissions.Read), Categories.RolePermission, null, "read");
        Register(nameof(Security.RolePermissions.Write), Categories.RolePermission, null, "write");
        Register(nameof(Security.RolePermissions.Delete), Categories.RolePermission, null, "delete");
        Register(nameof(Security.RolePermissions.Execute), Categories.RolePermission, null, "execute");
        Register(nameof(Security.RolePermissions.Admin), Categories.RolePermission, null, "admin");

        // 安全配置键
        Register(nameof(Security.ConfigKeys.EnableInputValidation), Categories.SecurityConfig, null, "Security:EnableInputValidation");
        Register(nameof(Security.ConfigKeys.EnablePermissionCheck), Categories.SecurityConfig, null, "Security:EnablePermissionCheck");
        Register(nameof(Security.ConfigKeys.EnableSecurityLogging), Categories.SecurityConfig, null, "Security:EnableSecurityLogging");
        Register(nameof(Security.ConfigKeys.EnableMaliciousDetection), Categories.SecurityConfig, null, "Security:EnableMaliciousDetection");
        Register(nameof(Security.ConfigKeys.MaxInputLength), Categories.SecurityConfig, null, "Security:MaxInputLength");
        Register(nameof(Security.ConfigKeys.AllowedTools), Categories.SecurityConfig, null, "Security:AllowedTools");

        // 安全限制
        RegisterInt(nameof(Security.Limits.MaxInputLength), Categories.SecurityLimit, null, 100000);
        RegisterInt(nameof(Security.Limits.MaxArrayLength), Categories.SecurityLimit, null, 1000);
        RegisterInt(nameof(Security.Limits.MaxStringLength), Categories.SecurityLimit, null, 50000);
        RegisterInt(nameof(Security.Limits.MaxParameterCount), Categories.SecurityLimit, null, 50);
        RegisterInt(nameof(Security.Limits.MaxNestingDepth), Categories.SecurityLimit, null, 10);

        // 消息模板
        Register(nameof(MessageTemplates.BusyPrefix), Categories.MessageTemplate, null, "[BUSY]");
        Register(nameof(MessageTemplates.LockTimeout), Categories.MessageTemplate, null, "{0} Lock timeout after {1} seconds");
        Register(nameof(MessageTemplates.LockTimeoutWrite), Categories.MessageTemplate, null, "{0} Failed to write to {1}");
        Register(nameof(MessageTemplates.LockTimeoutSave), Categories.MessageTemplate, null, "{0} Failed to save to {1}");
        Register(nameof(MessageTemplates.PartialBusy), Categories.MessageTemplate, null, "{0} Partially completed: {1} {2} processed. {3}");
        Register(nameof(MessageTemplates.DeletedButBusy), Categories.MessageTemplate, null, "{0} {1} deleted but {2}");

        // 验证消息
        Register(nameof(ValidationMessages.MissingRequiredParameter), Categories.ValidationMessage, null, "缺少必需参数: {0}");
        Register(nameof(ValidationMessages.ParameterTypeMismatch), Categories.ValidationMessage, null, "参数类型不匹配: 期望 {0}, 实际 {1}");
    }

    #region 注册方法

    private static void Register(string name, string category, string? subCategory, string value, string? description = null)
    {
        var metadata = new ConstantMetadata
        {
            Name = name,
            Category = category,
            SubCategory = subCategory,
            Value = value,
            ValueType = typeof(string),
            Description = description
        };
        AddToIndex(metadata);
    }

    private static void RegisterInt(string name, string category, string? subCategory, int value, string? description = null)
    {
        var metadata = new ConstantMetadata
        {
            Name = name,
            Category = category,
            SubCategory = subCategory,
            Value = value,
            ValueType = typeof(int),
            Description = description
        };
        AddToIndex(metadata);
    }

    private static void RegisterChar(string name, string category, string? subCategory, char value, string? description = null)
    {
        var metadata = new ConstantMetadata
        {
            Name = name,
            Category = category,
            SubCategory = subCategory,
            Value = value,
            ValueType = typeof(char),
            Description = description
        };
        AddToIndex(metadata);
    }

    private static void RegisterTimeSpan(string name, string category, string? subCategory, TimeSpan value, string? description = null)
    {
        var metadata = new ConstantMetadata
        {
            Name = name,
            Category = category,
            SubCategory = subCategory,
            Value = value,
            ValueType = typeof(TimeSpan),
            Description = description
        };
        AddToIndex(metadata);
    }

    private static void AddToIndex(ConstantMetadata metadata)
    {
        _constants[metadata.Name] = metadata;

        if (!_categoryIndex.TryGetValue(metadata.Category, out var subCategories))
        {
            subCategories = new Dictionary<string, List<ConstantMetadata>>(StringComparer.Ordinal);
            _categoryIndex[metadata.Category] = subCategories;
        }

        var key = metadata.SubCategory ?? string.Empty;
        if (!subCategories.TryGetValue(key, out var list))
        {
            list = new List<ConstantMetadata>();
            subCategories[key] = list;
        }

        list.Add(metadata);
    }

    #endregion

    #region 查询方法

    /// <summary>
    /// 获取常量值
    /// </summary>
    public static T? GetValue<T>(string name)
    {
        if (_constants.TryGetValue(name, out var metadata))
        {
            if (metadata.Value is T typedValue)
            {
                return typedValue;
            }
        }
        return default;
    }

    /// <summary>
    /// 获取字符串常量值
    /// </summary>
    public static string GetString(string name) => GetValue<string>(name) ?? string.Empty;

    /// <summary>
    /// 获取整型常量值
    /// </summary>
    public static int GetInt(string name) => GetValue<int>(name);

    /// <summary>
    /// 获取字符常量值
    /// </summary>
    public static char GetChar(string name) => GetValue<char>(name);

    /// <summary>
    /// 获取TimeSpan常量值
    /// </summary>
    public static TimeSpan GetTimeSpan(string name) => GetValue<TimeSpan>(name);

    /// <summary>
    /// 获取常量元数据
    /// </summary>
    public static ConstantMetadata? GetMetadata(string name)
    {
        return _constants.TryGetValue(name, out var metadata) ? metadata : null;
    }

    /// <summary>
    /// 按分类获取常量
    /// </summary>
    public static IEnumerable<ConstantMetadata> GetByCategory(string category, string? subCategory = null)
    {
        if (_categoryIndex.TryGetValue(category, out var subCategories))
        {
            var key = subCategory ?? string.Empty;
            if (subCategories.TryGetValue(key, out var list))
            {
                return list;
            }
        }
        return Enumerable.Empty<ConstantMetadata>();
    }

    /// <summary>
    /// 获取所有分类
    /// </summary>
    public static IEnumerable<string> GetCategories() => _categoryIndex.Keys;

    /// <summary>
    /// 获取指定分类下的所有子分类
    /// </summary>
    public static IEnumerable<string> GetSubCategories(string category)
    {
        if (_categoryIndex.TryGetValue(category, out var subCategories))
        {
            return subCategories.Keys.Where(k => !string.IsNullOrEmpty(k));
        }
        return Enumerable.Empty<string>();
    }

    /// <summary>
    /// 检查常量是否存在
    /// </summary>
    public static bool Exists(string name) => _constants.ContainsKey(name);

    /// <summary>
    /// 获取所有常量名称
    /// </summary>
    public static IEnumerable<string> GetAllNames() => _constants.Keys;

    #endregion

    #region 分类常量

    /// <summary>
    /// 分类名称常量
    /// </summary>
    public static class Categories
    {
        public const string File = nameof(File);
        public const string Directory = nameof(Directory);
        public const string Timeout = nameof(Timeout);
        public const string Retry = nameof(Retry);
        public const string Separator = nameof(Separator);
        public const string Platform = nameof(Platform);
        public const string JsonRpc = nameof(JsonRpc);
        public const string ErrorCode = nameof(ErrorCode);
        public const string Suffix = nameof(Suffix);
        public const string DateTimeFormat = nameof(DateTimeFormat);
        public const string JsonValueType = nameof(JsonValueType);
        public const string ContentType = nameof(ContentType);
        public const string Version = nameof(Version);
        public const string ProjectPath = nameof(ProjectPath);
        public const string Command = nameof(Command);
        public const string LogLevel = nameof(LogLevel);
        public const string MutexName = nameof(MutexName);
        public const string SecurityEvent = nameof(SecurityEvent);
        public const string AttackType = nameof(AttackType);
        public const string PermissionLevel = nameof(PermissionLevel);
        public const string RolePermission = nameof(RolePermission);
        public const string SecurityConfig = nameof(SecurityConfig);
        public const string SecurityLimit = nameof(SecurityLimit);
        public const string MessageTemplate = nameof(MessageTemplate);
        public const string ValidationMessage = nameof(ValidationMessage);
    }

    /// <summary>
    /// 子分类名称常量
    /// </summary>
    public static class SubCategories
    {
        public const string Extension = nameof(Extension);
        public const string Name = nameof(Name);
        public const string MemoryCommand = nameof(MemoryCommand);
        public const string McpCommand = nameof(McpCommand);
        public const string CliCommand = nameof(CliCommand);
        public const string FileReaderCommand = nameof(FileReaderCommand);
    }

    #endregion

    #region 类型化访问器

    /// <summary>
    /// 文件扩展名
    /// </summary>
    public static class FileExtensions
    {
        public static string Jsonl => GetString(nameof(Jsonl));
        public static string Json => GetString(nameof(Json));
        public static string Exe => GetString(nameof(Exe));
        public static string Tgz => GetString(nameof(Tgz));
    }

    /// <summary>
    /// 文件名
    /// </summary>
    public static class FileNames
    {
        public static string Memory => GetString(nameof(Memory));
        public static string Relations => GetString(nameof(Relations));
        public static string Tools => GetString(nameof(Tools));
        public static string Npm => GetString(nameof(Npm));
        public static string NpmCmd => GetString(nameof(NpmCmd));
    }

    /// <summary>
    /// 目录名
    /// </summary>
    public static class DirectoryNames
    {
        public static string Cache => GetString(nameof(Cache));
        public static string Tools => GetString(nameof(Tools));
        public static string Cli => GetString(nameof(Cli));
        public static string Logs => GetString(nameof(Logs));
        public static string Plugins => GetString(nameof(Plugins));
    }

    /// <summary>
    /// 超时设置
    /// </summary>
    public static class Timeouts
    {
        public static TimeSpan DefaultCommand => GetTimeSpan(nameof(DefaultCommand));
        public static TimeSpan DefaultLock => GetTimeSpan(nameof(DefaultLock));
        public static TimeSpan ProcessExitDelay => GetTimeSpan(nameof(ProcessExitDelay));
    }

    /// <summary>
    /// 重试策略
    /// </summary>
    public static class RetryPolicy
    {
        public static int MaxRetries => GetInt(nameof(MaxRetries));
        public static int BaseDelayMs => GetInt(nameof(BaseDelayMs));
    }

    /// <summary>
    /// 分隔符
    /// </summary>
    public static class Separators
    {
        public static char RelationKey => GetChar(nameof(RelationKey));
        public static string PackagePrefix => GetString(nameof(PackagePrefix));
    }

    /// <summary>
    /// 平台标识
    /// </summary>
    public static class Platforms
    {
        public static string Windows => GetString(nameof(Windows));
        public static string Linux => GetString(nameof(Linux));
        public static string OSX => GetString(nameof(OSX));
        public static string Unknown => GetString(nameof(Unknown));
    }

    /// <summary>
    /// JSON-RPC
    /// </summary>
    public static class JsonRpc
    {
        public static string ContentLengthPrefix => GetString(nameof(ContentLengthPrefix));
        public static string ProtocolVersion => GetString(nameof(ProtocolVersion));
    }

    /// <summary>
    /// 错误码
    /// </summary>
    public static class ErrorCodes
    {
        public static int MethodNotFound => GetInt(nameof(MethodNotFound));
        public static int InternalError => GetInt(nameof(InternalError));
    }

    /// <summary>
    /// 命名后缀
    /// </summary>
    public static class Suffixes
    {
        public static string Service => GetString(nameof(Service));
        public static string Provider => GetString(nameof(Provider));
        public static string Repository => GetString(nameof(Repository));
    }

    /// <summary>
    /// 日期时间格式
    /// </summary>
    public static class DateTimeFormats
    {
        public static string Timestamp => GetString(nameof(Timestamp));
        public static string FileTimestamp => GetString(nameof(FileTimestamp));
        public static string LogFile => GetString(nameof(LogFile));
        public static string LogEntry => GetString(nameof(LogEntry));
    }

    /// <summary>
    /// JSON值类型
    /// </summary>
    public static class JsonValueTypes
    {
        public static string ObjectType => GetString(nameof(ObjectType));
        public static string StringType => GetString(nameof(StringType));
        public static string IntegerType => GetString(nameof(IntegerType));
        public static string Number => GetString(nameof(Number));
        public static string Boolean => GetString(nameof(Boolean));
        public static string Array => GetString(nameof(Array));
    }

    /// <summary>
    /// 内容类型
    /// </summary>
    public static class ContentTypes
    {
        public static string Text => GetString(nameof(Text));
    }

    /// <summary>
    /// 版本号
    /// </summary>
    public static class Versions
    {
        public static string McpHost => GetString(nameof(McpHost));
    }

    /// <summary>
    /// 项目路径
    /// </summary>
    public static class ProjectPaths
    {
        public static string BinDirectory => GetString(nameof(BinDirectory));
        public static string ReleaseConfiguration => GetString(nameof(ReleaseConfiguration));
        public static string DebugConfiguration => GetString(nameof(DebugConfiguration));
        public static string TargetFramework => GetString(nameof(TargetFramework));
        public static string WindowsX64Runtime => GetString(nameof(WindowsX64Runtime));
        public static string PublishDirectory => GetString(nameof(PublishDirectory));
    }

    /// <summary>
    /// 命令名称
    /// </summary>
    public static class Commands
    {
        public static class Memory
        {
            public static string CreateEntities => GetString(nameof(CreateEntities));
            public static string CreateRelations => GetString(nameof(CreateRelations));
            public static string ReadGraph => GetString(nameof(ReadGraph));
            public static string SearchNodes => GetString(nameof(SearchNodes));
            public static string AddObservations => GetString(nameof(AddObservations));
            public static string DeleteEntities => GetString(nameof(DeleteEntities));
            public static string DeleteObservations => GetString(nameof(DeleteObservations));
            public static string DeleteRelations => GetString(nameof(DeleteRelations));
            public static string OpenNodes => GetString(nameof(OpenNodes));
            public static string GetStorageInfo => GetString(nameof(GetStorageInfo));
            public static string ListTools => GetString(nameof(ListTools));
        }

        public static class Mcp
        {
            public static string Initialize => GetString(nameof(Initialize));
            public static string ToolsList => GetString(nameof(ToolsList));
            public static string ToolsCall => GetString(nameof(ToolsCall));
            public static string Initialized => GetString(nameof(Initialized));
        }

        public static class Cli
        {
            public static string Help => GetString(nameof(Help));
            public static string HelpShort => GetString(nameof(HelpShort));
            public static string HelpWindows => GetString(nameof(HelpWindows));
            public static string JsonInput => GetString(nameof(JsonInput));
            public static string Command => GetString(nameof(Command));
        }

        public static class FileReader
        {
            public static string ReadHead => GetString(nameof(ReadHead));
            public static string ReadTail => GetString(nameof(ReadTail));
            public static string ListTools => GetString(nameof(ListTools));
        }
    }

    /// <summary>
    /// 消息模板
    /// </summary>
    public static class MessageTemplates
    {
        public static string BusyPrefix => GetString(nameof(BusyPrefix));
        public static string LockTimeout => GetString(nameof(LockTimeout));
        public static string LockTimeoutWrite => GetString(nameof(LockTimeoutWrite));
        public static string LockTimeoutSave => GetString(nameof(LockTimeoutSave));
        public static string PartialBusy => GetString(nameof(PartialBusy));
        public static string DeletedButBusy => GetString(nameof(DeletedButBusy));
    }

    /// <summary>
    /// 验证消息
    /// </summary>
    public static class ValidationMessages
    {
        public static string MissingRequiredParameter => GetString(nameof(MissingRequiredParameter));
        public static string ParameterTypeMismatch => GetString(nameof(ParameterTypeMismatch));
    }

    /// <summary>
    /// 日志级别
    /// </summary>
    public static class LogLevels
    {
        public static string Debug => GetString(nameof(Debug));
        public static string Info => GetString(nameof(Info));
        public static string Warn => GetString(nameof(Warn));
        public static string Error => GetString(nameof(Error));
        public static string Unknown => GetString(nameof(Unknown));
    }

    /// <summary>
    /// 互斥锁名称
    /// </summary>
    public static class MutexNames
    {
        public static string McpHostSingleInstance => GetString(nameof(McpHostSingleInstance));

        public static string GetMemoryCliLock(string baseDirectory)
        {
            var hash = Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(baseDirectory));
            return $"Global\\MemoryCli_{hash}_Lock";
        }
    }

    /// <summary>
    /// 安全相关常量
    /// </summary>
    public static class Security
    {
        public static class EventTypes
        {
            public static string InputValidationFailed => GetString(nameof(InputValidationFailed));
            public static string PermissionDenied => GetString(nameof(PermissionDenied));
            public static string MaliciousContentDetected => GetString(nameof(MaliciousContentDetected));
            public static string ToolExecutionBlocked => GetString(nameof(ToolExecutionBlocked));
            public static string WhitelistViolation => GetString(nameof(WhitelistViolation));
            public static string SchemaValidationFailed => GetString(nameof(SchemaValidationFailed));
            public static string UnauthorizedAccess => GetString(nameof(UnauthorizedAccess));
        }

        public static class AttackTypes
        {
            public static string SqlInjection => GetString(nameof(SqlInjection));
            public static string CommandInjection => GetString(nameof(CommandInjection));
            public static string Xss => GetString(nameof(Xss));
            public static string PathTraversal => GetString(nameof(PathTraversal));
            public static string JsonInjection => GetString(nameof(JsonInjection));
            public static string ScriptInjection => GetString(nameof(ScriptInjection));
        }

        public static class PermissionLevels
        {
            public static string Admin => GetString(nameof(Admin));
            public static string PowerUser => GetString(nameof(PowerUser));
            public static string User => GetString(nameof(User));
            public static string Guest => GetString(nameof(Guest));
        }

        public static class RolePermissions
        {
            public static string Read => GetString(nameof(Read));
            public static string Write => GetString(nameof(Write));
            public static string Delete => GetString(nameof(Delete));
            public static string Execute => GetString(nameof(Execute));
            public static string Admin => GetString(nameof(Admin));
        }

        public static class ConfigKeys
        {
            public static string EnableInputValidation => GetString(nameof(EnableInputValidation));
            public static string EnablePermissionCheck => GetString(nameof(EnablePermissionCheck));
            public static string EnableSecurityLogging => GetString(nameof(EnableSecurityLogging));
            public static string EnableMaliciousDetection => GetString(nameof(EnableMaliciousDetection));
            public static string MaxInputLength => GetString(nameof(MaxInputLength));
            public static string AllowedTools => GetString(nameof(AllowedTools));
        }

        public static class Limits
        {
            public static int MaxInputLength => GetInt(nameof(MaxInputLength));
            public static int MaxArrayLength => GetInt(nameof(MaxArrayLength));
            public static int MaxStringLength => GetInt(nameof(MaxStringLength));
            public static int MaxParameterCount => GetInt(nameof(MaxParameterCount));
            public static int MaxNestingDepth => GetInt(nameof(MaxNestingDepth));
        }

        /// <summary>
        /// 恶意内容检测模式
        /// </summary>
        public static class MaliciousPatterns
        {
            public static readonly string[] SqlInjectionPatterns =
            [
                @"(--)",
                @"(/\*.*\*/)",
                @"(\bOR\b\s+\d+\s*=\s*\d+)",
                @"(\bAND\b\s+\d+\s*=\s*\d+)",
                @"(UNION\s+SELECT)",
                @"(EXEC\s*\()",
                @"(EXECUTE\s*\()",
                @"('\s*OR\s+[']?\d+[']?\s*=\s*[']?\d+)",
                @"('\s*AND\s+[']?\d+[']?\s*=\s*[']?\d+)",
                @"('\s*;)",
                @"(admin'--)",
                @"(\bDROP\s+TABLE\b)",
                @"(\bDELETE\s+FROM\b)",
                @"(1'\s*=\s*'1)"
            ];

            public static readonly string[] CommandInjectionPatterns =
            [
                @"(\||\|\||&&|;|\$\(|`)",
                @"(\b(cmd|powershell|bash|sh|exec|eval)\b)",
                @"(echo|cat|ls|dir|rm|del|cp|copy|mv|move|grep|find)\s+[^|<>]*>[\s\w./\\]+",
                @"(cat|sort|uniq|wc)\s*<[\s\w./\\]+",
                @"(^\s*>>?\s*/\w+)",
                @"(^\s*<\s*/\w+)",
                @"(2>&1)",
                @"(\.\.\/|\.\.\\)",
                @"(&\s+\w+)",
                @"(\|\s*\w+)",
                @"(wmic\s+)",
                @"(certutil\s+)",
                @"(net\s+user)",
                @"(reg\s+save)",
                @"(\$\(\([^)]+\)\))",
                @"(\(\([^)]+\)\))"
            ];

            public static readonly string[] XssPatterns =
            [
                @"(<script[^>]*>.*?</script>)",
                @"(<script[^>]*>)",
                @"(javascript\s*:)",
                @"(\s+on(?:click|dblclick|mousedown|mouseup|mouseover|mousemove|mouseout|keydown|keypress|keyup|focus|blur|load|unload|submit|reset|select|change|error|abort|resize|scroll|contextmenu|drag|drop|copy|cut|paste)\s*=)",
                @"(<iframe[^>]*>)",
                @"(<object[^>]*>)",
                @"(<embed[^>]*>)",
                @"(<svg\s+[^>]*on(?:load|error|click))",
                @"(<\w+\s+[^>]*onerror\s*=)"
            ];

            public static readonly string[] PathTraversalPatterns =
            [
                @"(\.\.\/)",
                @"(\.\.\\)",
                @"(%2e%2e%2f)",
                @"(%2e%2e\/)",
                @"(\.\.%2f)",
                @"(%2e%2e%5c)",
                @"(%252e%252e%252f)",
                @"(%252e%252e%255c)",
                @"(%252f)",
                @"(^/etc/)",
                @"(^/var/)",
                @"(^/usr/)",
                @"(^/bin/)",
                @"(^/sbin/)",
                @"(^C:\\Windows)",
                @"(^C:\\Program\s+Files)"
            ];

            public static readonly string[] EnvironmentVariablePatterns =
            [
                @"(\$\{[A-Za-z_]\w*\})",
                @"(\$[A-Za-z_]\w*)",
                @"(%[A-Za-z_]\w*%)"
            ];
        }
    }

    #endregion
}
