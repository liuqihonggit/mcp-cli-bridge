using Common.Security;
using Common.Contracts.Security;
using Common.Contracts.Models;

namespace MyMemoryServer.SecurityTests;

/// <summary>
/// 权限控制测试 - 测试权限拒绝场景
/// </summary>
public sealed class PermissionControlTests
{
    #region 白名单测试

    [Fact]
    public void IsToolAllowed_WithWhitelistEnabledAndAllowedTool_ShouldReturnTrue()
    {
        // Arrange
        var whitelist = new WhitelistConfiguration
        {
            IsEnabled = true,
            AllowedTools = new HashSet<string> { "tool1", "tool2", "tool3" }
        };

        var inputValidator = new Common.Security.Validation.JsonSchemaValidator();
        var permissionChecker = new Common.Security.Permissions.WhitelistPermissionChecker(whitelist, new RbacConfiguration { IsEnabled = false });
        var validator = new SecurityValidator(inputValidator, permissionChecker, whitelist);

        // Act & Assert
        validator.IsToolAllowed("tool1").Should().BeTrue();
        validator.IsToolAllowed("tool2").Should().BeTrue();
        validator.IsToolAllowed("tool3").Should().BeTrue();
    }

    [Fact]
    public void IsToolAllowed_WithWhitelistEnabledAndNotAllowedTool_ShouldReturnFalse()
    {
        // Arrange
        var whitelist = new WhitelistConfiguration
        {
            IsEnabled = true,
            AllowedTools = new HashSet<string> { "tool1", "tool2" }
        };

        var inputValidator = new Common.Security.Validation.JsonSchemaValidator();
        var permissionChecker = new Common.Security.Permissions.WhitelistPermissionChecker(whitelist, new RbacConfiguration { IsEnabled = false });
        var validator = new SecurityValidator(inputValidator, permissionChecker, whitelist);

        // Act & Assert
        validator.IsToolAllowed("tool3").Should().BeFalse();
        validator.IsToolAllowed("malicious_tool").Should().BeFalse();
    }

    [Fact]
    public void IsToolAllowed_WithWhitelistDisabled_ShouldReturnTrue()
    {
        // Arrange
        var whitelist = new WhitelistConfiguration
        {
            IsEnabled = false,
            AllowedTools = new HashSet<string> { "tool1" }
        };

        var inputValidator = new Common.Security.Validation.JsonSchemaValidator();
        var permissionChecker = new Common.Security.Permissions.WhitelistPermissionChecker(whitelist, new RbacConfiguration { IsEnabled = false });
        var validator = new SecurityValidator(inputValidator, permissionChecker, whitelist);

        // Act & Assert
        validator.IsToolAllowed("any_tool").Should().BeTrue();
        validator.IsToolAllowed("malicious_tool").Should().BeTrue();
    }

    [Fact]
    public async Task CheckPermission_WithWhitelistViolation_ShouldReturnDenied()
    {
        // Arrange
        var whitelist = new WhitelistConfiguration
        {
            IsEnabled = true,
            AllowedTools = new HashSet<string> { "allowed_tool" }
        };

        var inputValidator = new Common.Security.Validation.JsonSchemaValidator();
        var permissionChecker = new Common.Security.Permissions.WhitelistPermissionChecker(whitelist, new RbacConfiguration { IsEnabled = false });
        var validator = new SecurityValidator(inputValidator, permissionChecker, whitelist);

        var context = new SecurityContext { UserId = "user1" };

        // Act
        var result = await validator.CheckPermissionAsync("blocked_tool", context);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.DenyReason.Should().Contain("不在白名单中");
    }

    #endregion

    #region RBAC权限测试

    [Fact]
    public async Task CheckPermission_WithAdminRole_ShouldAllowAllTools()
    {
        // Arrange
        var permissionCheckerMock = new Mock<IPermissionChecker>();
        permissionCheckerMock
            .Setup(p => p.CheckPermissionAsync(It.IsAny<PermissionCheckRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(PermissionResult.Allowed()));

        var inputValidator = new Common.Security.Validation.JsonSchemaValidator();
        var validator = new SecurityValidator(inputValidator, permissionCheckerMock.Object);

        var context = new SecurityContext
        {
            UserId = "admin",
            Roles = new List<string> { "admin" }
        };

        // Act
        var result = await validator.CheckPermissionAsync("any_tool", context);

        // Assert
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckPermission_WithInsufficientRole_ShouldDeny()
    {
        // Arrange
        var permissionCheckerMock = new Mock<IPermissionChecker>();
        permissionCheckerMock
            .Setup(p => p.CheckPermissionAsync(It.IsAny<PermissionCheckRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(PermissionResult.Denied("Insufficient permissions", "PERMISSION_DENIED")));

        var inputValidator = new Common.Security.Validation.JsonSchemaValidator();
        var validator = new SecurityValidator(inputValidator, permissionCheckerMock.Object);

        var context = new SecurityContext
        {
            UserId = "user1",
            Roles = new List<string> { "reader" }
        };

        // Act
        var result = await validator.CheckPermissionAsync("admin_tool", context);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.DenyReason.Should().Contain("Insufficient permissions");
    }

    [Fact]
    public async Task CheckPermission_WithNoRoles_ShouldDeny()
    {
        // Arrange
        var permissionCheckerMock = new Mock<IPermissionChecker>();
        permissionCheckerMock
            .Setup(p => p.CheckPermissionAsync(It.IsAny<PermissionCheckRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(PermissionResult.Denied("No roles assigned", "NO_ROLES")));

        var inputValidator = new Common.Security.Validation.JsonSchemaValidator();
        var validator = new SecurityValidator(inputValidator, permissionCheckerMock.Object);

        var context = new SecurityContext
        {
            UserId = "user1",
            Roles = new List<string>()
        };

        // Act
        var result = await validator.CheckPermissionAsync("any_tool", context);

        // Assert
        result.IsAllowed.Should().BeFalse();
    }

    #endregion

    #region 用户权限测试

    [Fact]
    public async Task CheckPermission_WithNoUserId_ShouldAllow()
    {
        // Arrange - 无用户信息时默认允许
        var inputValidator = new Common.Security.Validation.JsonSchemaValidator();
        var permissionChecker = new Common.Security.Permissions.WhitelistPermissionChecker(
            new WhitelistConfiguration { IsEnabled = false },
            new RbacConfiguration { IsEnabled = false });
        var validator = new SecurityValidator(inputValidator, permissionChecker);

        var context = new SecurityContext
        {
            UserId = null,
            Roles = new List<string>()
        };

        // Act
        var result = await validator.CheckPermissionAsync("any_tool", context);

        // Assert
        result.IsAllowed.Should().BeTrue("无用户信息时应默认允许");
    }

    [Fact]
    public async Task CheckPermission_WithSpecificUserPermission_ShouldAllow()
    {
        // Arrange
        var permissionCheckerMock = new Mock<IPermissionChecker>();
        permissionCheckerMock
            .Setup(p => p.CheckPermissionAsync(
                It.Is<PermissionCheckRequest>(r => r.UserId == "user1" && r.ToolName == "specific_tool"),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(PermissionResult.Allowed()));

        var inputValidator = new Common.Security.Validation.JsonSchemaValidator();
        var validator = new SecurityValidator(inputValidator, permissionCheckerMock.Object);

        var context = new SecurityContext
        {
            UserId = "user1",
            Roles = new List<string> { "user" }
        };

        // Act
        var result = await validator.CheckPermissionAsync("specific_tool", context);

        // Assert
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
        // Arrange
        var permissionCheckerMock = new Mock<IPermissionChecker>();
        permissionCheckerMock
            .Setup(p => p.CheckPermissionAsync(It.IsAny<PermissionCheckRequest>(), It.IsAny<CancellationToken>()))
            .Returns((PermissionCheckRequest req, CancellationToken _) =>
            {
                // 模拟权限规则：只有admin可以删除
                if (req.ToolName.Contains("delete") && !req.Roles.Contains("admin"))
                {
                    return Task.FromResult(PermissionResult.Denied("Only admin can delete", "PERMISSION_DENIED"));
                }
                return Task.FromResult(PermissionResult.Allowed());
            });

        var inputValidator = new Common.Security.Validation.JsonSchemaValidator();
        var validator = new SecurityValidator(inputValidator, permissionCheckerMock.Object);

        var context = new SecurityContext
        {
            UserId = "testuser",
            Roles = new List<string> { role }
        };

        // Act
        var result = await validator.CheckPermissionAsync(toolName, context);

        // Assert
        result.IsAllowed.Should().Be(expectedAllowed);
    }

    #endregion

    #region 安全上下文测试

    [Fact]
    public void SecurityContext_WithEmptyRoles_ShouldBeValid()
    {
        // Arrange & Act
        var context = new SecurityContext
        {
            UserId = "user1",
            Roles = new List<string>()
        };

        // Assert
        context.UserId.Should().Be("user1");
        context.Roles.Should().BeEmpty();
    }

    [Fact]
    public void SecurityContext_WithMultipleRoles_ShouldContainAll()
    {
        // Arrange & Act
        var context = new SecurityContext
        {
            UserId = "user1",
            Roles = new List<string> { "admin", "user", "editor" }
        };

        // Assert
        context.Roles.Should().HaveCount(3);
        context.Roles.Should().Contain("admin");
        context.Roles.Should().Contain("user");
        context.Roles.Should().Contain("editor");
    }

    #endregion

    #region 权限检查结果测试

    [Fact]
    public void PermissionResult_Allowed_ShouldReturnCorrectResult()
    {
        // Act
        var result = PermissionResult.Allowed();

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.DenyReason.Should().BeNull();
    }

    [Fact]
    public void PermissionResult_Denied_ShouldReturnCorrectResult()
    {
        // Act
        var result = PermissionResult.Denied("Access denied", "ACCESS_DENIED");

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.DenyReason.Should().Be("Access denied");
    }

    #endregion
}
