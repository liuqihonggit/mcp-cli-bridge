using Common.Security;
using Common.Contracts.Security;
using Common.Contracts.Models;

namespace MyMemoryServer.SecurityTests;

public sealed class PermissionControlTests
{
    #region 白名单测试

    [Fact]
    public void IsToolInWhitelist_WithWhitelistEnabledAndAllowedTool_ShouldReturnTrue()
    {
        var whitelist = new WhitelistConfiguration
        {
            IsEnabled = true,
            AllowedTools = new HashSet<string> { "tool1", "tool2", "tool3" }
        };

        var permissionChecker = new Common.Security.Permissions.RbacPermissionChecker(new RbacConfiguration { IsEnabled = false }, whitelist);

        permissionChecker.IsToolInWhitelist("tool1").Should().BeTrue();
        permissionChecker.IsToolInWhitelist("tool2").Should().BeTrue();
        permissionChecker.IsToolInWhitelist("tool3").Should().BeTrue();
    }

    [Fact]
    public void IsToolInWhitelist_WithWhitelistEnabledAndNotAllowedTool_ShouldReturnFalse()
    {
        var whitelist = new WhitelistConfiguration
        {
            IsEnabled = true,
            AllowedTools = new HashSet<string> { "tool1", "tool2" }
        };

        var permissionChecker = new Common.Security.Permissions.RbacPermissionChecker(new RbacConfiguration { IsEnabled = false }, whitelist);

        permissionChecker.IsToolInWhitelist("tool3").Should().BeFalse();
        permissionChecker.IsToolInWhitelist("malicious_tool").Should().BeFalse();
    }

    [Fact]
    public void IsToolInWhitelist_WithWhitelistDisabled_ShouldReturnTrue()
    {
        var whitelist = new WhitelistConfiguration
        {
            IsEnabled = false,
            AllowedTools = new HashSet<string> { "tool1" }
        };

        var permissionChecker = new Common.Security.Permissions.RbacPermissionChecker(new RbacConfiguration { IsEnabled = false }, whitelist);

        permissionChecker.IsToolInWhitelist("any_tool").Should().BeTrue();
        permissionChecker.IsToolInWhitelist("malicious_tool").Should().BeTrue();
    }

    [Fact]
    public async Task CheckPermission_WithWhitelistViolation_ShouldReturnDenied()
    {
        var whitelist = new WhitelistConfiguration
        {
            IsEnabled = true,
            AllowedTools = new HashSet<string> { "allowed_tool" }
        };

        var permissionChecker = new Common.Security.Permissions.RbacPermissionChecker(new RbacConfiguration { IsEnabled = false }, whitelist);

        var request = new PermissionCheckRequest
        {
            ToolName = "blocked_tool",
            UserId = "user1"
        };

        var result = await permissionChecker.CheckPermissionAsync(request);
        result.IsAllowed.Should().BeFalse();
        result.DenyReason.Should().Contain("不在白名单中");
    }

    #endregion

    #region RBAC权限测试

    [Fact]
    public async Task CheckPermission_WithAdminRole_ShouldAllowAllTools()
    {
        var permissionCheckerMock = new Mock<IPermissionChecker>();
        permissionCheckerMock
            .Setup(p => p.CheckPermissionAsync(It.IsAny<PermissionCheckRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(PermissionResult.Allowed()));

        var request = new PermissionCheckRequest
        {
            ToolName = "any_tool",
            UserId = "admin",
            Roles = new List<string> { "admin" }
        };

        var result = await permissionCheckerMock.Object.CheckPermissionAsync(request);
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckPermission_WithInsufficientRole_ShouldDeny()
    {
        var permissionCheckerMock = new Mock<IPermissionChecker>();
        permissionCheckerMock
            .Setup(p => p.CheckPermissionAsync(It.IsAny<PermissionCheckRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(PermissionResult.Denied("Insufficient permissions", "PERMISSION_DENIED")));

        var request = new PermissionCheckRequest
        {
            ToolName = "admin_tool",
            UserId = "user1",
            Roles = new List<string> { "reader" }
        };

        var result = await permissionCheckerMock.Object.CheckPermissionAsync(request);
        result.IsAllowed.Should().BeFalse();
        result.DenyReason.Should().Contain("Insufficient permissions");
    }

    [Fact]
    public async Task CheckPermission_WithNoRoles_ShouldDeny()
    {
        var permissionCheckerMock = new Mock<IPermissionChecker>();
        permissionCheckerMock
            .Setup(p => p.CheckPermissionAsync(It.IsAny<PermissionCheckRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(PermissionResult.Denied("No roles assigned", "NO_ROLES")));

        var request = new PermissionCheckRequest
        {
            ToolName = "any_tool",
            UserId = "user1",
            Roles = new List<string>()
        };

        var result = await permissionCheckerMock.Object.CheckPermissionAsync(request);
        result.IsAllowed.Should().BeFalse();
    }

    #endregion

    #region 用户权限测试

    [Fact]
    public async Task CheckPermission_WithNoUserId_ShouldAllow()
    {
        var permissionChecker = new Common.Security.Permissions.RbacPermissionChecker(
            new RbacConfiguration { IsEnabled = false },
            new WhitelistConfiguration { IsEnabled = false });

        var request = new PermissionCheckRequest
        {
            ToolName = "any_tool",
            UserId = null
        };

        var result = await permissionChecker.CheckPermissionAsync(request);
        result.IsAllowed.Should().BeTrue("无用户信息时应默认允许");
    }

    [Fact]
    public async Task CheckPermission_WithSpecificUserPermission_ShouldAllow()
    {
        var permissionCheckerMock = new Mock<IPermissionChecker>();
        permissionCheckerMock
            .Setup(p => p.CheckPermissionAsync(
                It.Is<PermissionCheckRequest>(r => r.UserId == "user1" && r.ToolName == "specific_tool"),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(PermissionResult.Allowed()));

        var request = new PermissionCheckRequest
        {
            ToolName = "specific_tool",
            UserId = "user1",
            Roles = new List<string> { "user" }
        };

        var result = await permissionCheckerMock.Object.CheckPermissionAsync(request);
        result.IsAllowed.Should().BeTrue();
    }

    #endregion

    #region 工具特定权限测试

    [Theory]
    [InlineData("memory_create_entities", "user", true)]
    [InlineData("memory_delete_entities", "user", false)]
    [InlineData("memory_create_entities", "admin", true)]
    [InlineData("memory_delete_entities", "admin", true)]
    public async Task CheckPermission_WithToolSpecificPermissions_ShouldEnforceCorrectly(
        string toolName, string role, bool expectedAllowed)
    {
        var permissionCheckerMock = new Mock<IPermissionChecker>();
        permissionCheckerMock
            .Setup(p => p.CheckPermissionAsync(It.IsAny<PermissionCheckRequest>(), It.IsAny<CancellationToken>()))
            .Returns((PermissionCheckRequest req, CancellationToken _) =>
            {
                if (req.ToolName.Contains("delete") && !req.Roles.Contains("admin"))
                {
                    return Task.FromResult(PermissionResult.Denied("Only admin can delete", "PERMISSION_DENIED"));
                }
                return Task.FromResult(PermissionResult.Allowed());
            });

        var request = new PermissionCheckRequest
        {
            ToolName = toolName,
            UserId = "testuser",
            Roles = new List<string> { role }
        };

        var result = await permissionCheckerMock.Object.CheckPermissionAsync(request);
        result.IsAllowed.Should().Be(expectedAllowed);
    }

    #endregion

    #region 权限检查结果测试

    [Fact]
    public void PermissionResult_Allowed_ShouldReturnCorrectResult()
    {
        var result = PermissionResult.Allowed();
        result.IsAllowed.Should().BeTrue();
        result.DenyReason.Should().BeNull();
    }

    [Fact]
    public void PermissionResult_Denied_ShouldReturnCorrectResult()
    {
        var result = PermissionResult.Denied("Access denied", "ACCESS_DENIED");
        result.IsAllowed.Should().BeFalse();
        result.DenyReason.Should().Be("Access denied");
    }

    #endregion
}
