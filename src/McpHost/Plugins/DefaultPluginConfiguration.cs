namespace McpHost.Plugins;

/// <summary>
/// 默认插件配置 - 替代 plugins.json 的代码配置
/// </summary>
public static class DefaultPluginConfiguration
{
    /// <summary>
    /// 创建默认插件配置（空配置，等待动态发现）
    /// </summary>
    public static PluginConfiguration CreateDefault()
    {
        return new PluginConfiguration
        {
            Version = "1.0.0",
            Providers = []
        };
    }

    /// <summary>
    /// 创建 CLI 提供者配置
    /// </summary>
    /// <param name="name">提供者名称</param>
    /// <param name="cliCommand">CLI命令名称</param>
    /// <param name="processPoolSize">进程池大小</param>
    /// <param name="timeout">超时时间</param>
    public static CliProviderConfiguration CreateCliProvider(
        string name,
        string cliCommand,
        int processPoolSize = 5,
        TimeSpan? timeout = null)
    {
        return new CliProviderConfiguration
        {
            Type = nameof(CliToolProvider),
            Name = name,
            CliCommand = cliCommand,
            Timeout = (timeout ?? TimeSpan.FromSeconds(30)).ToString(),
            ProcessPoolSize = processPoolSize,
            Tools = []
        };
    }
}
