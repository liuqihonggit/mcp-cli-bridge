namespace Common.Contracts.Security;

/// <summary>
/// 权限检查器接口，负责检查用户权限
/// </summary>
public interface IPermissionChecker
{
    /// <summary>
    /// 异步检查权限
    /// </summary>
    /// <param name="request">权限检查请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限检查结果</returns>
    Task<PermissionResult> CheckPermissionAsync(
        PermissionCheckRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查工具是否在白名单中
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <returns>是否在白名单中</returns>
    bool IsToolInWhitelist(string toolName);

    /// <summary>
    /// 获取用户的所有权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>权限列表</returns>
    IReadOnlyList<string> GetUserPermissions(string userId);

    /// <summary>
    /// 检查用户是否具有指定角色
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="role">角色名称</param>
    /// <returns>是否具有角色</returns>
    bool UserHasRole(string userId, string role);
}
