using Common.Contracts.Models;

namespace Common.Contracts.PluginManager;

/// <summary>
/// 工具提供者接口，定义CLI工具提供者的核心能力
/// </summary>
public interface IToolProvider
{
    /// <summary>
    /// 提供者名称，用于标识和日志记录
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// 插件元数据（从CLI的list_tools返回），包含描述、分类、命令数等信息
    /// </summary>
    PluginDescriptor? PluginMetadata { get; }

    /// <summary>
    /// 获取此提供者支持的所有工具元数据
    /// </summary>
    /// <returns>工具元数据集合</returns>
    IReadOnlyList<IToolMetadata> GetAvailableTools();

    /// <summary>
    /// 异步执行指定工具
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="parameters">工具参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    Task<OperationResult> ExecuteAsync(
        string toolName,
        IReadOnlyDictionary<string, JsonElement> parameters,
        CancellationToken cancellationToken = default);
}
