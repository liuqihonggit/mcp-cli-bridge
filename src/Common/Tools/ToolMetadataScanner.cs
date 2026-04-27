using System.Diagnostics.CodeAnalysis;
using McpProtocol.Contracts;

namespace Common.Tools;

/// <summary>
/// 工具元数据扫描器，用于反射扫描带特性的类
/// </summary>
public static class ToolMetadataScanner
{
    /// <summary>
    /// 扫描指定程序集中的所有工具元数据
    /// </summary>
    /// <param name="assembly">要扫描的程序集</param>
    /// <returns>工具元数据信息列表</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "工具类型由TrimmerRootAssembly保留")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "工具类型是AOT兼容的")]
    public static IReadOnlyList<ToolMetadataInfo> ScanAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var toolTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetCustomAttribute<ToolMetadataAttribute>() != null)
            .ToList();

        return toolTypes
            .Select(CreateToolMetadataInfo)
            .ToList();
    }

    /// <summary>
    /// 扫描多个程序集中的所有工具元数据
    /// </summary>
    /// <param name="assemblies">要扫描的程序集集合</param>
    /// <returns>工具元数据信息列表</returns>
    public static IReadOnlyList<ToolMetadataInfo> ScanAssemblies(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return assemblies
            .SelectMany(ScanAssembly)
            .ToList();
    }

    /// <summary>
    /// 扫描当前应用程序域中的所有工具元数据
    /// </summary>
    /// <returns>工具元数据信息列表</returns>
    public static IReadOnlyList<ToolMetadataInfo> ScanCurrentDomain()
    {
        return ScanAssemblies(AppDomain.CurrentDomain.GetAssemblies());
    }

    /// <summary>
    /// 根据命令名称查找工具元数据
    /// </summary>
    /// <param name="assembly">要扫描的程序集</param>
    /// <param name="commandName">命令名称</param>
    /// <returns>工具元数据信息，如果未找到则返回null</returns>
    public static ToolMetadataInfo? FindByCommandName(Assembly assembly, string commandName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrEmpty(commandName);

        return ScanAssembly(assembly)
            .FirstOrDefault(t => t.CommandName.Equals(commandName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 根据工具名称查找工具元数据
    /// </summary>
    /// <param name="assembly">要扫描的程序集</param>
    /// <param name="toolName">工具名称</param>
    /// <returns>工具元数据信息，如果未找到则返回null</returns>
    public static ToolMetadataInfo? FindByToolName(Assembly assembly, string toolName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrEmpty(toolName);

        return ScanAssembly(assembly)
            .FirstOrDefault(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 创建工具元数据信息
    /// </summary>
    private static ToolMetadataInfo CreateToolMetadataInfo(Type toolType)
    {
        var metadata = toolType.GetCustomAttribute<ToolMetadataAttribute>()!;
        var schema = toolType.GetCustomAttribute<ToolSchemaAttribute>();
        var examples = toolType.GetCustomAttributes<ToolExampleAttribute>().ToList();

        return new ToolMetadataInfo
        {
            Name = metadata.Name,
            Description = metadata.Description,
            CommandName = metadata.CommandName,
            Category = metadata.Category,
            ToolType = toolType,
            InputSchema = schema?.CreateSchema(metadata.CommandName) ?? CreateDefaultSchema(metadata.CommandName),
            Examples = examples.Select(e => new ToolExample
            {
                Title = e.Title,
                Description = e.Description,
                JsonRequest = e.JsonRequest
            }).ToList()
        };
    }

    /// <summary>
    /// 创建默认Schema（仅包含command字段）
    /// </summary>
    private static JsonElement CreateDefaultSchema(string commandName)
    {
        var schema = new JsonSchemaBuilder()
            .WithProperty("command", new JsonSchemaPropertyBuilder()
                .WithType("string")
                .WithConst(commandName)
                .Build())
            .WithRequired("command")
            .Build();

        return JsonSchemaSerializer.SerializeToJsonElement(schema);
    }
}

/// <summary>
/// 工具元数据信息
/// </summary>
public sealed class ToolMetadataInfo
{
    /// <summary>
    /// 工具名称
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 工具描述
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 工具命令名称
    /// </summary>
    public string CommandName { get; init; } = string.Empty;

    /// <summary>
    /// 工具类别
    /// </summary>
    public string Category { get; init; } = "general";

    /// <summary>
    /// 工具类型
    /// </summary>
    public Type ToolType { get; init; } = typeof(object);

    /// <summary>
    /// 输入参数的JSON Schema
    /// </summary>
    public JsonElement InputSchema { get; init; }

    /// <summary>
    /// 工具示例列表
    /// </summary>
    public IReadOnlyList<ToolExample> Examples { get; init; } = [];

    public ExtendedToolDefinition ToToolDefinition()
    {
        return new ExtendedToolDefinition
        {
            Name = Name,
            Description = Description,
            Category = Category,
            InputSchema = InputSchema
        };
    }
}

public sealed class ExtendedToolDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = "general";
    public JsonElement InputSchema { get; init; }
}
