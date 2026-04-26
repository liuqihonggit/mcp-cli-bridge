namespace Common.Plugins;

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
