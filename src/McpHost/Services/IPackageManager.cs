namespace McpHost.Services;

public interface IPackageManager
{
    string GetToolsDirectory();
    string? GetExecutablePath(string toolName);
    Task<bool> DownloadPackageAsync(string packageName, string? version = null);

    /// <summary>
    /// 发现所有可用的 CLI 插件
    /// </summary>
    IReadOnlyList<string> DiscoverAvailablePlugins();
}
