using Common.Tools;
using SecurityConstants = Common.Constants.ConstantManager.Security;

namespace Common.Security.Configuration;

/// <summary>
/// 安全配置加载器
/// </summary>
public static class SecurityConfigurationLoader
{
    /// <summary>
    /// 从JSON文件加载白名单配置
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>白名单配置</returns>
    public static async Task<WhitelistConfiguration> LoadWhitelistConfigurationAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var config = await FileOperationHelper.ReadJsonAsync(
            filePath,
            CommonJsonContext.Default.WhitelistConfigurationJsonModel,
            cancellationToken);

        if (config is null)
        {
            return CreateDefaultWhitelistConfiguration();
        }

        return new WhitelistConfiguration
        {
            AllowedTools = config.AllowedTools?.ToHashSet() ?? [],
            AllowedCliCommands = config.AllowedCliCommands?.ToHashSet() ?? [],
            IsEnabled = config.IsEnabled
        };
    }

    /// <summary>
    /// 从JSON文件加载RBAC配置
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>RBAC配置</returns>
    public static async Task<RbacConfiguration> LoadRbacConfigurationAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var config = await FileOperationHelper.ReadJsonAsync(
            filePath,
            CommonJsonContext.Default.RbacConfigurationJsonModel,
            cancellationToken);

        if (config is null)
        {
            return CreateDefaultRbacConfiguration();
        }

        return new RbacConfiguration
        {
            RolePermissions = config.RolePermissions?.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToHashSet()) ?? [],
            UserRoles = config.UserRoles?.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToHashSet()) ?? [],
            IsEnabled = config.IsEnabled
        };
    }

    /// <summary>
    /// 创建默认白名单配置
    /// </summary>
    public static WhitelistConfiguration CreateDefaultWhitelistConfiguration()
    {
        return new WhitelistConfiguration
        {
            AllowedTools =
            {
                "memory_create_entities",
                "memory_create_relations",
                "memory_read_graph",
                "memory_search_nodes",
                "memory_add_observations",
                "memory_delete_entities",
                "memory_open_nodes"
            },
            AllowedCliCommands = { "MemoryCli" },
            IsEnabled = true
        };
    }

    /// <summary>
    /// 创建默认RBAC配置
    /// </summary>
    public static RbacConfiguration CreateDefaultRbacConfiguration()
    {
        return new RbacConfiguration
        {
            RolePermissions = new Dictionary<string, HashSet<string>>
            {
                ["admin"] =
                [
                    "read",
                    "write",
                    "delete",
                    "execute",
                    "admin"
                ],
                ["poweruser"] =
                [
                    "read",
                    "write",
                    "execute"
                ],
                ["user"] =
                [
                    "read",
                    "execute"
                ],
                ["guest"] =
                [
                    "read"
                ]
            },
            UserRoles = new Dictionary<string, HashSet<string>>(),
            IsEnabled = true
        };
    }

    /// <summary>
    /// 保存白名单配置到文件
    /// </summary>
    /// <param name="config">配置</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task SaveWhitelistConfigurationAsync(WhitelistConfiguration config, string filePath, CancellationToken cancellationToken = default)
    {
        var json = new WhitelistConfigurationJsonModel
        {
            AllowedTools = config.AllowedTools.ToList(),
            AllowedCliCommands = config.AllowedCliCommands.ToList(),
            IsEnabled = config.IsEnabled
        };

        await FileOperationHelper.WriteJsonAsync(
            filePath,
            json,
            CommonJsonContext.Default.WhitelistConfigurationJsonModel,
            cancellationToken);
    }

    /// <summary>
    /// 保存RBAC配置到文件
    /// </summary>
    /// <param name="config">配置</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task SaveRbacConfigurationAsync(RbacConfiguration config, string filePath, CancellationToken cancellationToken = default)
    {
        var json = new RbacConfigurationJsonModel
        {
            RolePermissions = config.RolePermissions.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToList()),
            UserRoles = config.UserRoles.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToList()),
            IsEnabled = config.IsEnabled
        };

        await FileOperationHelper.WriteJsonAsync(
            filePath,
            json,
            CommonJsonContext.Default.RbacConfigurationJsonModel,
            cancellationToken);
    }
}
