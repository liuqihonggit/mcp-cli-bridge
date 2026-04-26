namespace Common.Tools;

/// <summary>
/// 标记一个方法为MCP工具方法
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class McpToolAttribute : Attribute
{
    /// <summary>
    /// 工具名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 工具描述
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 工具命令名称，用于关联增强描述
    /// </summary>
    public string? CommandName { get; init; }

    /// <summary>
    /// 工具类别，用于分组管理
    /// </summary>
    public string Category { get; init; } = "general";

    /// <summary>
    /// 默认超时时间（毫秒）
    /// </summary>
    public int DefaultTimeout { get; init; } = 30000;

    /// <summary>
    /// 执行此工具所需的权限列表（逗号分隔）
    /// </summary>
    public string? RequiredPermissions { get; init; }

    /// <summary>
    /// 是否启用增强描述
    /// </summary>
    public bool EnableEnhancedDescription { get; init; } = true;

    /// <summary>
    /// 初始化MCP工具特性
    /// </summary>
    /// <param name="name">工具名称</param>
    /// <param name="description">工具描述</param>
    public McpToolAttribute(string name, string description)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(description);

        Name = name;
        Description = description;
    }

    /// <summary>
    /// 获取增强描述，如果配置了CommandName则从预定义描述中获取
    /// </summary>
    public EnhancedToolDescription? GetEnhancedDescription()
    {
        if (!EnableEnhancedDescription)
            return null;

        if (!string.IsNullOrEmpty(CommandName))
            return MemoryToolDescriptions.GetDescription(CommandName);

        return null;
    }

    /// <summary>
    /// 获取权限列表
    /// </summary>
    public IReadOnlyList<string> GetRequiredPermissions()
    {
        if (string.IsNullOrEmpty(RequiredPermissions))
            return [];

        return RequiredPermissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

/// <summary>
/// 标记工具方法的参数属性
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
public sealed class McpParameterAttribute : Attribute
{
    /// <summary>
    /// 参数描述
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 参数是否必需
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// 参数默认值（JSON格式）
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// 初始化MCP参数特性
    /// </summary>
    /// <param name="description">参数描述</param>
    public McpParameterAttribute(string description)
    {
        ArgumentException.ThrowIfNullOrEmpty(description);
        Description = description;
    }
}
