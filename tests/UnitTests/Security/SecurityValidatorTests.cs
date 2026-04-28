using Common.Contracts.Security;

namespace MyMemoryServer.UnitTests.Security;

public sealed class SecurityValidatorTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly Mock<IInputValidator> _inputValidatorMock;
    private readonly Mock<IPermissionChecker> _permissionCheckerMock;
    private readonly SecurityValidator _validator;

    public SecurityValidatorTests()
    {
        _loggerMock = new Mock<ILogger>();
        _inputValidatorMock = new Mock<IInputValidator>();
        _permissionCheckerMock = new Mock<IPermissionChecker>();
        _validator = new SecurityValidator(_inputValidatorMock.Object, _permissionCheckerMock.Object);
    }

    [Fact]
    public void ValidateInput_WithValidInput_ShouldReturnSuccess()
    {
        // Arrange
        var parameters = new Dictionary<string, JsonElement>
        {
            ["key"] = JsonSerializer.SerializeToElement("value")
        };

        // Act
        var result = _validator.ValidateInput("test_tool", parameters);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateInput_WithInvalidInput_ShouldReturnFailure()
    {
        // Arrange
        var parameters = new Dictionary<string, JsonElement>
        {
            ["key"] = JsonSerializer.SerializeToElement("malicious<script>")
        };

        // Act
        var result = _validator.ValidateInput("test_tool", parameters);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("恶意"));
    }

    [Fact]
    public async Task CheckPermission_WithAllowedTool_ShouldReturnAllowed()
    {
        // Arrange
        _permissionCheckerMock
            .Setup(p => p.CheckPermissionAsync(It.IsAny<PermissionCheckRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PermissionResult.Allowed());

        var context = new SecurityContext { UserId = "user1", Roles = ["user"] };

        // Act
        var result = await _validator.CheckPermissionAsync("allowed_tool", context);

        // Assert
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckPermission_WithDeniedTool_ShouldReturnDenied()
    {
        // Arrange
        _permissionCheckerMock
            .Setup(p => p.CheckPermissionAsync(It.IsAny<PermissionCheckRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PermissionResult.Denied("Not allowed", "PERMISSION_DENIED"));

        var context = new SecurityContext { UserId = "user1", Roles = ["user"] };

        // Act
        var result = await _validator.CheckPermissionAsync("denied_tool", context);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.DenyReason.Should().Contain("Not allowed");
    }
}
