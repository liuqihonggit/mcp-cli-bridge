using Common.Contracts.Security;

namespace MyMemoryServer.UnitTests.Security;

public sealed class JsonSchemaValidatorTests
{
    private readonly Common.Security.Validation.JsonSchemaValidator _validator;

    public JsonSchemaValidatorTests()
    {
        _validator = new Common.Security.Validation.JsonSchemaValidator();
    }

    [Fact]
    public void DetectMaliciousContent_WithXssAttack_ShouldReturnFailure()
    {
        var result = _validator.DetectMaliciousContent("malicious<script>");
        result.IsValid.Should().BeFalse();
        result.DetectedAttacks.Should().NotBeEmpty();
    }

    [Fact]
    public void DetectMaliciousContent_WithValidContent_ShouldReturnSuccess()
    {
        var result = _validator.DetectMaliciousContent("hello world");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithValidInput_ShouldReturnSuccess()
    {
        var parameters = new Dictionary<string, JsonElement>
        {
            ["key"] = JsonSerializer.SerializeToElement("value")
        };

        var request = new InputValidationRequest
        {
            ToolName = "test_tool",
            Parameters = parameters
        };

        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithMaliciousInput_ShouldReturnFailure()
    {
        var parameters = new Dictionary<string, JsonElement>
        {
            ["key"] = JsonSerializer.SerializeToElement("malicious<script>")
        };

        var request = new InputValidationRequest
        {
            ToolName = "test_tool",
            Parameters = parameters
        };

        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
    }
}
