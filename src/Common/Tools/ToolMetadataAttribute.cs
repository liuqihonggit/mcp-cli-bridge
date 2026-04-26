namespace Common.Tools;

/// <summary>
/// 工具元数据特性，用于标记工具类并定义工具的元数据信息
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ToolMetadataAttribute : Attribute
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
    /// 工具命令名称，用于关联命令处理
    /// </summary>
    public string CommandName { get; }

    /// <summary>
    /// 工具类别，用于分组管理
    /// </summary>
    public string Category { get; init; } = "general";

    /// <summary>
    /// 初始化工具元数据特性
    /// </summary>
    /// <param name="name">工具名称</param>
    /// <param name="description">工具描述</param>
    /// <param name="commandName">工具命令名称</param>
    public ToolMetadataAttribute(string name, string description, string commandName)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(description);
        ArgumentException.ThrowIfNullOrEmpty(commandName);

        Name = name;
        Description = description;
        CommandName = commandName;
    }
}

/// <summary>
/// 工具示例特性，用于定义工具的JSON请求示例
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class ToolExampleAttribute : Attribute
{
    /// <summary>
    /// 示例标题
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 示例描述
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// JSON请求示例内容
    /// </summary>
    public string JsonRequest { get; }

    /// <summary>
    /// 初始化工具示例特性
    /// </summary>
    /// <param name="title">示例标题</param>
    /// <param name="description">示例描述</param>
    /// <param name="jsonRequest">JSON请求示例</param>
    public ToolExampleAttribute(string title, string description, string jsonRequest)
    {
        ArgumentException.ThrowIfNullOrEmpty(title);
        ArgumentException.ThrowIfNullOrEmpty(description);
        ArgumentException.ThrowIfNullOrEmpty(jsonRequest);

        Title = title;
        Description = description;
        JsonRequest = jsonRequest;
    }
}

/// <summary>
/// 工具Schema特性，用于定义工具的JSON Schema
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ToolSchemaAttribute : Attribute
{
    /// <summary>
    /// Schema属性定义列表
    /// </summary>
    public IReadOnlyList<SchemaPropertyDefinition> Properties { get; }

    /// <summary>
    /// 必填字段列表
    /// </summary>
    public IReadOnlyList<string> Required { get; }

    /// <summary>
    /// 使用预定义属性初始化Schema特性
    /// </summary>
    /// <param name="properties">属性定义数组</param>
    /// <param name="required">必填字段数组</param>
    public ToolSchemaAttribute(SchemaPropertyDefinition[] properties, string[] required)
    {
        Properties = properties ?? [];
        Required = required ?? [];
    }

    /// <summary>
    /// 创建JsonElement格式的Schema
    /// </summary>
    public JsonElement CreateSchema(string commandName)
    {
        var builder = new JsonSchemaBuilder();

        // 添加command属性
        builder.WithProperty("command", new JsonSchemaPropertyBuilder()
            .WithType("string")
            .WithConst(commandName)
            .Build());

        // 添加其他属性
        foreach (var property in Properties)
        {
            builder.WithProperty(property.Name, CreateSchemaProperty(property));
        }

        // 设置必填字段
        var allRequired = new List<string> { "command" };
        allRequired.AddRange(Required);
        builder.WithRequired(allRequired.ToArray());

        return JsonSchemaSerializer.SerializeToJsonElement(builder.Build());
    }

    private static JsonSchemaProperty CreateSchemaProperty(SchemaPropertyDefinition definition)
    {
        var builder = new JsonSchemaPropertyBuilder()
            .WithType(definition.Type);

        if (!string.IsNullOrEmpty(definition.Description))
        {
            builder.WithDescription(definition.Description);
        }

        if (definition.Items != null)
        {
            builder.WithItems(CreateSchemaProperty(definition.Items));
        }

        if (definition.Properties != null && definition.Properties.Count > 0)
        {
            var properties = definition.Properties.ToDictionary(
                p => p.Name,
                CreateSchemaProperty);
            builder.WithProperties(properties);
        }

        if (definition.Required != null && definition.Required.Count > 0)
        {
            builder.WithRequired(definition.Required.ToArray());
        }

        return builder.Build();
    }
}

/// <summary>
/// Schema属性定义
/// </summary>
public sealed class SchemaPropertyDefinition
{
    /// <summary>
    /// 属性名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 属性类型
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// 属性描述
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// 数组项定义（仅当Type为array时有效）
    /// </summary>
    public SchemaPropertyDefinition? Items { get; }

    /// <summary>
    /// 对象属性定义（仅当Type为object时有效）
    /// </summary>
    public IReadOnlyList<SchemaPropertyDefinition>? Properties { get; }

    /// <summary>
    /// 必填子字段（仅当Type为object时有效）
    /// </summary>
    public IReadOnlyList<string>? Required { get; }

    /// <summary>
    /// 初始化简单属性定义
    /// </summary>
    public SchemaPropertyDefinition(string name, string type, string? description = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(type);

        Name = name;
        Type = type;
        Description = description;
    }

    /// <summary>
    /// 初始化数组属性定义
    /// </summary>
    public SchemaPropertyDefinition(string name, string type, SchemaPropertyDefinition items, string? description = null)
        : this(name, type, description)
    {
        Items = items;
    }

    /// <summary>
    /// 初始化对象属性定义
    /// </summary>
    public SchemaPropertyDefinition(string name, string type, SchemaPropertyDefinition[] properties, string[] required, string? description = null)
        : this(name, type, description)
    {
        Properties = properties;
        Required = required;
    }
}
