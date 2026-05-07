using McpHost.Services;
using McpHost.Tools;

namespace MyMemoryServer.UnitTests.Tools;

public sealed class CliBridgeToolsTests : IDisposable
{
    private readonly Mock<IToolRegistry> _registryMock;
    private readonly Mock<IPackageManager> _packageManagerMock;
    private readonly Mock<ILogger> _loggerMock;
    private readonly CliBridgeTools _tools;

    public CliBridgeToolsTests()
    {
        _registryMock = new Mock<IToolRegistry>();
        _packageManagerMock = new Mock<IPackageManager>();
        _loggerMock = new Mock<ILogger>();
        _tools = new CliBridgeTools(_registryMock.Object, _packageManagerMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        _tools.Dispose();
    }

    #region tool_describe 二级调用测试

    [Fact]
    public async Task ToolDescribeAsync_WithValidPluginName_ShouldReturnCommands()
    {
        var toolMetadata = new Mock<IToolMetadata>();
        toolMetadata.SetupGet(m => m.Name).Returns("memory_create_entities");
        toolMetadata.SetupGet(m => m.Description).Returns("Create entities");
        toolMetadata.SetupGet(m => m.InputSchema).Returns(CreateTestSchema());

        _registryMock.Setup(r => r.GetPluginCommandsAsync("memory_cli"))
            .ReturnsAsync(new List<IToolMetadata> { toolMetadata.Object }.AsReadOnly());

        _registryMock.Setup(r => r.GetProviderMetadata("memory_cli"))
            .Returns(new PluginDescriptor
            {
                Name = "memory_cli",
                Description = "Memory CLI Plugin",
                Category = "knowledge-graph",
                CommandCount = 1,
                HasDocumentation = true
            });

        var result = await _tools.ToolDescribeAsync("memory_cli");

        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("memory_create_entities");
        result.Should().Contain("Memory CLI Plugin");
    }

    [Fact]
    public async Task ToolDescribeAsync_WithEmptyPluginName_ShouldReturnError()
    {
        var result = await _tools.ToolDescribeAsync("");

        result.Should().Contain("Plugin name cannot be empty");
    }

    [Fact]
    public async Task ToolDescribeAsync_WithNullPluginName_ShouldReturnError()
    {
        var result = await _tools.ToolDescribeAsync(null!);

        result.Should().Contain("Plugin name cannot be empty");
    }

    [Fact]
    public async Task ToolDescribeAsync_WithNonExistentPlugin_ShouldReturnError()
    {
        _registryMock.Setup(r => r.GetPluginCommandsAsync("nonexistent"))
            .ReturnsAsync(new List<IToolMetadata>().AsReadOnly());

        var result = await _tools.ToolDescribeAsync("nonexistent");

        result.Should().Contain("Plugin not found");
    }

    #endregion

    #region tool_execute 二级调用测试

    [Fact]
    public async Task ToolExecuteAsync_WithValidToolAndParameters_ShouldExecuteSuccessfully()
    {
        _registryMock.Setup(r => r.ExecuteToolAsync(
                "memory_create_entities",
                It.IsAny<IReadOnlyDictionary<string, JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationResult
            {
                Success = true,
                ExitCode = 0,
                Message = "Created 1 entity"
            });

        var parameters = new Dictionary<string, JsonElement>
        {
            ["command"] = JsonSerializer.SerializeToElement("create_entities"),
            ["entities"] = JsonSerializer.SerializeToElement(new[] { new { name = "TestEntity", entityType = "test" } })
        };

        var result = await _tools.ToolExecuteAsync("memory_create_entities", parameters);

        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Created 1 entity");

        _registryMock.Verify(r => r.ExecuteToolAsync(
            "memory_create_entities",
            It.IsAny<IReadOnlyDictionary<string, JsonElement>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToolExecuteAsync_WithEmptyToolName_ShouldReturnError()
    {
        var parameters = new Dictionary<string, JsonElement>
        {
            ["command"] = JsonSerializer.SerializeToElement("test")
        };

        var result = await _tools.ToolExecuteAsync("", parameters);

        result.Should().Contain("Tool name cannot be empty");
    }

    [Fact]
    public async Task ToolExecuteAsync_WithNullToolName_ShouldReturnError()
    {
        var parameters = new Dictionary<string, JsonElement>
        {
            ["command"] = JsonSerializer.SerializeToElement("test")
        };

        var result = await _tools.ToolExecuteAsync(null!, parameters);

        result.Should().Contain("Tool name cannot be empty");
    }

    [Fact]
    public async Task ToolExecuteAsync_WithEmptyParameters_ShouldReturnError()
    {
        var result = await _tools.ToolExecuteAsync("memory_create_entities", new Dictionary<string, JsonElement>());

        result.Should().Contain("Parameters cannot be empty");
    }

    [Fact]
    public async Task ToolExecuteAsync_WithNullParameters_ShouldReturnError()
    {
        var result = await _tools.ToolExecuteAsync("memory_create_entities", null!);

        result.Should().Contain("Parameters cannot be empty");
    }

    [Fact]
    public async Task ToolExecuteAsync_WithExecutionFailure_ShouldReturnError()
    {
        _registryMock.Setup(r => r.ExecuteToolAsync(
                "failing_tool",
                It.IsAny<IReadOnlyDictionary<string, JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("CLI process failed"));

        var parameters = new Dictionary<string, JsonElement>
        {
            ["command"] = JsonSerializer.SerializeToElement("fail")
        };

        var result = await _tools.ToolExecuteAsync("failing_tool", parameters);

        result.Should().Contain("Tool execution failed");
        result.Should().Contain("CLI process failed");
    }

    [Fact]
    public async Task ToolExecuteAsync_ParametersShouldBePassedToRegistry()
    {
        IReadOnlyDictionary<string, JsonElement>? capturedParams = null;

        _registryMock.Setup(r => r.ExecuteToolAsync(
                "test_tool",
                It.IsAny<IReadOnlyDictionary<string, JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyDictionary<string, JsonElement>, CancellationToken>((_, p, _) => capturedParams = p)
            .ReturnsAsync(new OperationResult
            {
                Success = true,
                ExitCode = 0,
                Message = "{\"success\":true}"
            });

        var parameters = new Dictionary<string, JsonElement>
        {
            ["command"] = JsonSerializer.SerializeToElement("list_tools"),
            ["extra"] = JsonSerializer.SerializeToElement("value")
        };

        await _tools.ToolExecuteAsync("test_tool", parameters);

        capturedParams.Should().NotBeNull();
        capturedParams.Should().ContainKey("command");
        capturedParams!["command"].GetString().Should().Be("list_tools");
        capturedParams.Should().ContainKey("extra");
        capturedParams["extra"].GetString().Should().Be("value");
    }

    #endregion

    #region tool_search 测试

    [Fact]
    public void ToolSearch_WithMatchingQuery_ShouldReturnResults()
    {
        _registryMock.Setup(r => r.GetProviderNames())
            .Returns(new List<string> { "memory_cli", "file_reader_cli" }.AsReadOnly());

        _registryMock.Setup(r => r.GetProviderMetadata("memory_cli"))
            .Returns(new PluginDescriptor { Name = "memory_cli", Description = "Knowledge Graph CLI", Category = "knowledge-graph", CommandCount = 10, HasDocumentation = true });

        _registryMock.Setup(r => r.GetProviderMetadata("file_reader_cli"))
            .Returns(new PluginDescriptor { Name = "file_reader_cli", Description = "File Reader CLI", Category = "file-operations", CommandCount = 2, HasDocumentation = true });

        var result = _tools.ToolSearch("memory");

        result.Should().Contain("memory_cli");
        result.Should().NotContain("file_reader_cli");
    }

    [Fact]
    public void ToolSearch_WithEmptyQuery_ShouldReturnError()
    {
        var result = _tools.ToolSearch("");

        result.Should().Contain("Query cannot be empty");
    }

    [Fact]
    public void ToolSearch_WithNoMatch_ShouldReturnError()
    {
        _registryMock.Setup(r => r.GetProviderNames())
            .Returns(new List<string> { "memory_cli" }.AsReadOnly());

        _registryMock.Setup(r => r.GetProviderMetadata("memory_cli"))
            .Returns(new PluginDescriptor { Name = "memory_cli", Description = "Knowledge Graph CLI", Category = "knowledge-graph", CommandCount = 10, HasDocumentation = true });

        var result = _tools.ToolSearch("nonexistent_xyz");

        result.Should().Contain("No plugins found");
    }

    #endregion

    #region tool_list 测试

    [Fact]
    public void ToolList_ShouldReturnAllProviders()
    {
        _registryMock.Setup(r => r.GetProviderNames())
            .Returns(new List<string> { "memory_cli", "file_reader_cli", "ast_cli" }.AsReadOnly());

        _registryMock.Setup(r => r.GetProviderMetadata(It.IsAny<string>()))
            .Returns((string name) => new PluginDescriptor { Name = name, Description = $"{name} Plugin", Category = "general", CommandCount = 1, HasDocumentation = true });

        var result = _tools.ToolList();

        result.Should().Contain("memory_cli");
        result.Should().Contain("file_reader_cli");
        result.Should().Contain("ast_cli");
        result.Should().Contain("totalPlugins\":3");
    }

    #endregion

    #region provider_list 测试

    [Fact]
    public void ProviderList_ShouldReturnProviderInfo()
    {
        _registryMock.Setup(r => r.GetProviderNames())
            .Returns(new List<string> { "memory_cli" }.AsReadOnly());

        _registryMock.Setup(r => r.GetProviderMetadata("memory_cli"))
            .Returns(new PluginDescriptor { Name = "memory_cli", Description = "Memory CLI", Category = "knowledge-graph", CommandCount = 10, HasDocumentation = true });

        var result = _tools.ProviderList();

        result.Should().Contain("memory_cli");
        result.Should().Contain("totalProviders\":1");
    }

    #endregion

    #region package_status 测试

    [Fact]
    public void PackageStatus_WithNoPackageName_ShouldReturnGlobalStatus()
    {
        _packageManagerMock.Setup(p => p.GetToolsDirectory())
            .Returns("/tools/dir");

        var result = _tools.PackageStatus(null);

        result.Should().Contain("isInstalled\":true");
        result.Should().Contain("all");
    }

    [Fact]
    public void PackageStatus_WithInstalledPackage_ShouldReturnInstalled()
    {
        _packageManagerMock.Setup(p => p.GetExecutablePath("memory_cli"))
            .Returns("/tools/memory_cli.exe");
        _packageManagerMock.Setup(p => p.GetToolsDirectory())
            .Returns("/tools/dir");

        var result = _tools.PackageStatus("memory_cli");

        result.Should().Contain("isInstalled\":true");
    }

    [Fact]
    public void PackageStatus_WithNotInstalledPackage_ShouldReturnNotInstalled()
    {
        _packageManagerMock.Setup(p => p.GetExecutablePath("nonexistent"))
            .Returns((string?)null);
        _packageManagerMock.Setup(p => p.GetToolsDirectory())
            .Returns("/tools/dir");

        var result = _tools.PackageStatus("nonexistent");

        result.Should().Contain("isInstalled\":false");
    }

    #endregion

    #region package_install 测试

    [Fact]
    public async Task PackageInstallAsync_WithEmptyName_ShouldReturnError()
    {
        var result = await _tools.PackageInstallAsync("");

        result.Should().Contain("Package name cannot be empty");
    }

    [Fact]
    public async Task PackageInstallAsync_WithValidName_ShouldCallPackageManager()
    {
        _packageManagerMock.Setup(p => p.DownloadPackageAsync("test-package", null))
            .ReturnsAsync(true);

        var result = await _tools.PackageInstallAsync("test-package");

        result.Should().Contain("installed successfully");
    }

    [Fact]
    public async Task PackageInstallAsync_WithFailedDownload_ShouldReturnFailure()
    {
        _packageManagerMock.Setup(p => p.DownloadPackageAsync("bad-package", null))
            .ReturnsAsync(false);

        var result = await _tools.PackageInstallAsync("bad-package");

        result.Should().Contain("Failed to install");
    }

    #endregion

    #region Helper Methods

    private static JsonElement CreateTestSchema()
    {
        var json = @"{""type"":""object"",""properties"":{""command"":{""type"":""string"",""const"":""create_entities""}},""required"":[""command""]}";
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    #endregion
}
