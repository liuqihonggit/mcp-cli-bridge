using Common.Security.Models;

using SecurityConstants = Common.Constants.ConstantManager.Security;

namespace Common.Security.Permissions;

/// <summary>
/// 白名单权限检查器实现
/// </summary>
public sealed class WhitelistPermissionChecker : IPermissionChecker
{
    private readonly WhitelistConfiguration _whitelistConfig;
    private readonly RbacConfiguration _rbacConfig;

    /// <summary>
    /// 初始化白名单权限检查器
    /// </summary>
    /// <param name="whitelistConfig">白名单配置</param>
    /// <param name="rbacConfig">RBAC配置</param>
    public WhitelistPermissionChecker(
        WhitelistConfiguration whitelistConfig,
        RbacConfiguration rbacConfig)
    {
        _whitelistConfig = whitelistConfig ?? throw new ArgumentNullException(nameof(whitelistConfig));
        _rbacConfig = rbacConfig ?? throw new ArgumentNullException(nameof(rbacConfig));
    }

    /// <inheritdoc />
    public Task<PermissionResult> CheckPermissionAsync(
        PermissionCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 检查白名单
        if (_whitelistConfig.IsEnabled && !IsToolInWhitelist(request.ToolName))
        {
            return Task.FromResult(PermissionResult.Denied(
                $"工具 '{request.ToolName}' 不在白名单中",
                SecurityConstants.EventTypes.WhitelistViolation));
        }

        // 检查RBAC权限
        if (_rbacConfig.IsEnabled && request.RequiredPermissions.Count > 0)
        {
            var userPermissions = GetUserPermissions(request.UserId ?? string.Empty);
            var missingPermissions = request.RequiredPermissions
                .Where(required => !userPermissions.Contains(required))
                .ToList();

            if (missingPermissions.Count > 0)
            {
                return Task.FromResult(PermissionResult.Denied(
                    $"缺少必要权限: {string.Join(", ", missingPermissions)}",
                    [.. missingPermissions]));
            }
        }

        return Task.FromResult(PermissionResult.Allowed());
    }

    /// <inheritdoc />
    public bool IsToolInWhitelist(string toolName)
    {
        if (!_whitelistConfig.IsEnabled)
        {
            return true;
        }

        return _whitelistConfig.AllowedTools.Contains(toolName);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetUserPermissions(string userId)
    {
        if (!_rbacConfig.IsEnabled || string.IsNullOrEmpty(userId))
        {
            return [];
        }

        // 获取用户角色
        var userRoles = _rbacConfig.UserRoles.TryGetValue(userId, out var roles)
            ? roles
            : [];

        // 聚合所有角色的权限
        var permissions = userRoles
            .SelectMany(role => _rbacConfig.RolePermissions.TryGetValue(role, out var rolePerms)
                ? rolePerms
                : [])
            .Distinct()
            .ToList();

        return permissions;
    }

    /// <inheritdoc />
    public bool UserHasRole(string userId, string role)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
        {
            return false;
        }

        return _rbacConfig.UserRoles.TryGetValue(userId, out var roles) && roles.Contains(role);
    }
}

/// <summary>
/// 基于角色的权限检查器实现
/// </summary>
public sealed class RbacPermissionChecker : IPermissionChecker
{
    private readonly RbacConfiguration _rbacConfig;
    private readonly WhitelistConfiguration _whitelistConfig;

    /// <summary>
    /// 初始化RBAC权限检查器
    /// </summary>
    /// <param name="rbacConfig">RBAC配置</param>
    /// <param name="whitelistConfig">白名单配置</param>
    public RbacPermissionChecker(
        RbacConfiguration rbacConfig,
        WhitelistConfiguration whitelistConfig)
    {
        _rbacConfig = rbacConfig ?? throw new ArgumentNullException(nameof(rbacConfig));
        _whitelistConfig = whitelistConfig ?? throw new ArgumentNullException(nameof(whitelistConfig));
    }

    /// <inheritdoc />
    public Task<PermissionResult> CheckPermissionAsync(
        PermissionCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 先检查白名单
        if (_whitelistConfig.IsEnabled && !IsToolInWhitelist(request.ToolName))
        {
            return Task.FromResult(PermissionResult.Denied(
                $"工具 '{request.ToolName}' 不在白名单中",
                SecurityConstants.EventTypes.WhitelistViolation));
        }

        // 检查RBAC权限
        if (!_rbacConfig.IsEnabled)
        {
            return Task.FromResult(PermissionResult.Allowed());
        }

        // 获取用户所有权限
        var userPermissions = GetUserPermissions(request.UserId ?? string.Empty);

        // 检查是否有所需权限
        var missingPermissions = request.RequiredPermissions
            .Where(required => !userPermissions.Contains(required))
            .ToList();

        if (missingPermissions.Count > 0)
        {
            return Task.FromResult(PermissionResult.Denied(
                $"用户缺少必要权限: {string.Join(", ", missingPermissions)}",
                [.. missingPermissions]));
        }

        // 检查角色级别权限
        var roleCheckResult = CheckRoleBasedPermissions(request);
        if (!roleCheckResult.IsAllowed)
        {
            return Task.FromResult(roleCheckResult);
        }

        return Task.FromResult(PermissionResult.Allowed());
    }

    /// <inheritdoc />
    public bool IsToolInWhitelist(string toolName)
    {
        if (!_whitelistConfig.IsEnabled)
        {
            return true;
        }

        return _whitelistConfig.AllowedTools.Contains(toolName);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetUserPermissions(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        // 获取用户角色
        if (!_rbacConfig.UserRoles.TryGetValue(userId, out var userRoles))
        {
            return [];
        }

        // 聚合所有角色的权限
        var permissions = new HashSet<string>();
        foreach (var role in userRoles)
        {
            if (_rbacConfig.RolePermissions.TryGetValue(role, out var rolePermissions))
            {
                foreach (var permission in rolePermissions)
                {
                    permissions.Add(permission);
                }
            }
        }

        return permissions.ToList();
    }

    /// <inheritdoc />
    public bool UserHasRole(string userId, string role)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
        {
            return false;
        }

        return _rbacConfig.UserRoles.TryGetValue(userId, out var roles) && roles.Contains(role);
    }

    /// <summary>
    /// 检查基于角色的权限
    /// </summary>
    private PermissionResult CheckRoleBasedPermissions(PermissionCheckRequest request)
    {
        if (string.IsNullOrEmpty(request.UserId))
        {
            return PermissionResult.Denied("未提供用户ID");
        }

        // 获取用户角色
        if (!_rbacConfig.UserRoles.TryGetValue(request.UserId, out var userRoles))
        {
            return PermissionResult.Denied($"用户 '{request.UserId}' 没有任何角色");
        }

        // 检查是否有管理员角色
        if (userRoles.Contains(SecurityConstants.PermissionLevels.Admin))
        {
            return PermissionResult.Allowed();
        }

        // 检查是否有PowerUser角色
        if (userRoles.Contains(SecurityConstants.PermissionLevels.PowerUser))
        {
            // PowerUser可以执行大部分操作，除了admin权限
            var adminOnlyPermissions = request.RequiredPermissions
                .Where(p => p == SecurityConstants.RolePermissions.Admin)
                .ToList();

            return adminOnlyPermissions.Count > 0
                ? PermissionResult.Denied("PowerUser无法执行管理员操作", [.. adminOnlyPermissions])
                : PermissionResult.Allowed();
        }

        // 检查普通用户权限
        if (userRoles.Contains(SecurityConstants.PermissionLevels.User))
        {
            // 普通用户只能执行read和execute权限
            var restrictedPermissions = request.RequiredPermissions
                .Where(p => p == SecurityConstants.RolePermissions.Delete ||
                           p == SecurityConstants.RolePermissions.Admin)
                .ToList();

            return restrictedPermissions.Count > 0
                ? PermissionResult.Denied("普通用户无法执行删除或管理员操作", [.. restrictedPermissions])
                : PermissionResult.Allowed();
        }

        // Guest用户只能执行read操作
        if (userRoles.Contains(SecurityConstants.PermissionLevels.Guest))
        {
            var allowedForGuest = new HashSet<string>
            {
                SecurityConstants.RolePermissions.Read,
                SecurityConstants.RolePermissions.Execute
            };

            var notAllowedForGuest = request.RequiredPermissions
                .Where(p => !allowedForGuest.Contains(p))
                .ToList();

            return notAllowedForGuest.Count > 0
                ? PermissionResult.Denied("Guest用户权限受限", [.. notAllowedForGuest])
                : PermissionResult.Allowed();
        }

        return PermissionResult.Denied("用户角色无效");
    }
}
