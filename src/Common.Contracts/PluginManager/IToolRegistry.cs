using Common.Contracts.Models;

namespace Common.Contracts.PluginManager;

/// <summary>
/// 工具注册中心接口，管理工具提供者的注册和工具发现
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// 注册工具提供者
    /// </summary>
    /// <param name="provider">工具提供者实例</param>
    void RegisterProvider(IToolProvider provider);

    /// <summary>
    /// 注销工具提供者
    /// </summary>
    /// <param name="providerName">提供者名称</param>
    /// <returns>是否成功注销</returns>
    bool UnregisterProvider(string providerName);

    /// <summary>
    /// 获取所有已注册工具的元数据
    /// </summary>
    /// <returns>所有工具元数据集合</returns>
    IReadOnlyList<IToolMetadata> GetAllTools();

    /// <summary>
    /// 按需获取指定插件的命令详情列表（渐进式发现）
    /// </summary>
    /// <param name="pluginName">插件/提供者名称</param>
    /// <returns>命令元数据列表，如果插件不存在返回空列表</returns>
    Task<IReadOnlyList<IToolMetadata>> GetPluginCommandsAsync(string pluginName);

    /// <summary>
    /// 获取指定提供者的插件元数据（从CLI的list_tools返回）
    /// </summary>
    /// <param name="providerName">提供者名称</param>
    /// <returns>插件描述符，如果提供者不存在或未发现则返回null</returns>
    PluginDescriptor? GetProviderMetadata(string providerName);

    /// <summary>
    /// 根据工具名称获取工具元数据
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="metadata">工具元数据（如果找到）</param>
    /// <returns>是否找到指定工具</returns>
    bool TryGetTool(string toolName, out IToolMetadata? metadata);

    /// <summary>
    /// 异步执行指定工具
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="parameters">工具参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    Task<OperationResult> ExecuteToolAsync(
        string toolName,
        IReadOnlyDictionary<string, JsonElement> parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有已注册的提供者名称
    /// </summary>
    /// <returns>提供者名称集合</returns>
    IReadOnlyList<string> GetProviderNames();
}
