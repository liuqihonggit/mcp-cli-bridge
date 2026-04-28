namespace Common.Json.Schema;

/// <summary>
/// JSON Schema 构建器
/// </summary>
public sealed class JsonSchemaBuilder
{
    private readonly JsonSchema _schema = new();

    /// <summary>
    /// 添加属性
    /// </summary>
    public JsonSchemaBuilder WithProperty(string name, JsonSchemaProperty property)
    {
        _schema.Properties[name] = property;
        return this;
    }

    /// <summary>
    /// 设置必填字段
    /// </summary>
    public JsonSchemaBuilder WithRequired(params string[] required)
    {
        _schema.Required.AddRange(required);
        return this;
    }

    /// <summary>
    /// 构建 Schema
    /// </summary>
    public JsonSchema Build() => _schema;
}

/// <summary>
/// JSON Schema 属性构建器
/// </summary>
public sealed class JsonSchemaPropertyBuilder
{
    private readonly JsonSchemaProperty _property = new();

    /// <summary>
    /// 设置类型
    /// </summary>
    public JsonSchemaPropertyBuilder WithType(string type)
    {
        _property.Type = type;
        return this;
    }

    /// <summary>
    /// 设置常量值
    /// </summary>
    public JsonSchemaPropertyBuilder WithConst(string constValue)
    {
        _property.Const = constValue;
        return this;
    }

    /// <summary>
    /// 设置描述
    /// </summary>
    public JsonSchemaPropertyBuilder WithDescription(string description)
    {
        _property.Description = description;
        return this;
    }

    /// <summary>
    /// 设置数组项类型
    /// </summary>
    public JsonSchemaPropertyBuilder WithItems(JsonSchemaProperty items)
    {
        _property.Items = items;
        return this;
    }

    /// <summary>
    /// 设置对象属性
    /// </summary>
    public JsonSchemaPropertyBuilder WithProperties(Dictionary<string, JsonSchemaProperty> properties)
    {
        _property.Properties = properties;
        return this;
    }

    /// <summary>
    /// 设置必填字段
    /// </summary>
    public JsonSchemaPropertyBuilder WithRequired(params string[] required)
    {
        _property.Required = required.ToList();
        return this;
    }

    /// <summary>
    /// 构建属性
    /// </summary>
    public JsonSchemaProperty Build() => _property;
}
