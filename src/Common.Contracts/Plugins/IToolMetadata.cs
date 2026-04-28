namespace Common.Contracts.Plugins;

/// <summary>
/// 工具元数据接口，定义工具的静态描述信息
/// </summary>
public interface IToolMetadata
{
    /// <summary>
    /// 工具名称，唯一标识符
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 工具描述，简要说明工具用途
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 工具类别，用于分组管理
    /// </summary>
    string Category { get; }

    /// <summary>
    /// 输入参数的JSON Schema定义
    /// </summary>
    JsonElement InputSchema { get; }

    /// <summary>
    /// 默认超时时间（毫秒）
    /// </summary>
    int DefaultTimeout { get; }

    /// <summary>
    /// 执行此工具所需的权限列表
    /// </summary>
    IReadOnlyList<string> RequiredPermissions { get; }
}
