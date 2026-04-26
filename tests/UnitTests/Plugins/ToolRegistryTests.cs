using Common.Contracts;

namespace MyMemoryServer.UnitTests.Plugins;

/// <summary>
/// 插件架构单元测试 — 渐进式发现架构
/// RegisterProvider 只注册 provider，不预加载 CLI 内部工具
/// 工具通过 GetPluginCommandsAsync / ExecuteAsync 按需获取
/// </summary>
public sealed class ToolRegistryTests : IDisposable
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly Mock<ICacheProvider> _cacheMock;
    private readonly ToolRegistry _registry;

    public ToolRegistryTests()
    {
        _loggerMock = new Mock<ILogger>();
        _cacheMock = new Mock<ICacheProvider>();
        _registry = new ToolRegistry(_loggerMock.Object, _cacheMock.Object);
    }

    public void Dispose()
    {
        _registry.GetType().GetMethod("Dispose", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .Invoke(_registry, null);
    }

    #region 工具提供者注册测试（渐进式架构：不预加载工具）

    [Fact]
    public void RegisterProvider_ShouldRegisterProviderWithoutPreloadingTools()
    {
        var providerMock = CreateMockProvider("TestProvider", ["tool1", "tool2"]);

        _registry.RegisterProvider(providerMock.Object);

        _registry.ProviderCount.Should().Be(1);
        _registry.ToolCount.Should().Be(0);
    }

    [Fact]
    public void RegisterProvider_WithDuplicateName_ShouldSkipRegistration()
    {
        var provider1 = CreateMockProvider("TestProvider", ["tool1"]);
        var provider2 = CreateMockProvider("TestProvider", ["tool2"]);

        _registry.RegisterProvider(provider1.Object);
        _registry.RegisterProvider(provider2.Object);

        _registry.ProviderCount.Should().Be(1);
        _registry.ToolCount.Should().Be(0);
    }

    [Fact]
    public void RegisterProvider_WithMultipleProviders_ShouldRegisterAll()
    {
        var provider1 = CreateMockProvider("Provider1", ["tool1", "tool2"]);
        var provider2 = CreateMockProvider("Provider2", ["tool3"]);

        _registry.RegisterProvider(provider1.Object);
        _registry.RegisterProvider(provider2.Object);

        _registry.ProviderCount.Should().Be(2);
        _registry.ToolCount.Should().Be(0);
    }

    [Fact]
    public void RegisterProvider_WithNullProvider_ShouldThrowArgumentNullException()
    {
        var act = () => _registry.RegisterProvider(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region 工具提供者注销测试

    [Fact]
    public void UnregisterProvider_ShouldRemoveProvider()
    {
        var provider = CreateMockProvider("TestProvider", ["tool1", "tool2"]);
        _registry.RegisterProvider(provider.Object);

        var result = _registry.UnregisterProvider("TestProvider");

        result.Should().BeTrue();
        _registry.ProviderCount.Should().Be(0);
        _registry.ToolCount.Should().Be(0);
    }

    [Fact]
    public void UnregisterProvider_WithNonExistentName_ShouldReturnFalse()
    {
        var result = _registry.UnregisterProvider("NonExistent");
        result.Should().BeFalse();
    }

    [Fact]
    public void UnregisterProvider_WithNullOrEmptyName_ShouldThrowArgumentException()
    {
        var act1 = () => _registry.UnregisterProvider(null!);
        var act2 = () => _registry.UnregisterProvider(string.Empty);
        var act3 = () => _registry.UnregisterProvider("   ");

        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
        act3.Should().Throw<ArgumentException>();
    }

    #endregion

    #region 渐进式发现测试（按需获取）

    [Fact]
    public async Task GetPluginCommandsAsync_ShouldLoadAndRegisterToolsOnDemand()
    {
        var provider1 = CreateMockProvider("Provider1", ["tool1", "tool2"]);
        var provider2 = CreateMockProvider("Provider2", ["tool3"]);
        _registry.RegisterProvider(provider1.Object);
        _registry.RegisterProvider(provider2.Object);

        var commands = await _registry.GetPluginCommandsAsync("Provider1");

        commands.Should().HaveCount(2);
        commands.Select(t => t.Name).Should().BeEquivalentTo(["tool1", "tool2"]);
        _registry.ToolCount.Should().Be(2);
    }

    [Fact]
    public async Task GetPluginCommandsAsync_WithNonExistentPlugin_ShouldReturnEmpty()
    {
        var commands = await _registry.GetPluginCommandsAsync("NonExistent");
        commands.Should().BeEmpty();
    }

    [Fact]
    public void GetAllTools_WithNoAccessedTools_ShouldReturnEmptyList()
    {
        var provider = CreateMockProvider("TestProvider", ["tool1", "tool2"]);
        _registry.RegisterProvider(provider.Object);

        var tools = _registry.GetAllTools();
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllTools_AfterPluginDiscovery_ShouldReturnDiscoveredTools()
    {
        var provider1 = CreateMockProvider("Provider1", ["tool1", "tool2"]);
        var provider2 = CreateMockProvider("Provider2", ["tool3"]);
        _registry.RegisterProvider(provider1.Object);
        _registry.RegisterProvider(provider2.Object);

        await _registry.GetPluginCommandsAsync("Provider1");
        await _registry.GetPluginCommandsAsync("Provider2");

        var tools = _registry.GetAllTools();
        tools.Should().HaveCount(3);
        tools.Select(t => t.Name).Should().BeEquivalentTo(["tool1", "tool2", "tool3"]);
    }

    [Fact]
    public async Task TryGetTool_AfterPluginDiscovery_ShouldReturnTrue()
    {
        var provider = CreateMockProvider("TestProvider", ["test_tool"]);
        _registry.RegisterProvider(provider.Object);

        await _registry.GetPluginCommandsAsync("TestProvider");

        var result = _registry.TryGetTool("test_tool", out var metadata);

        result.Should().BeTrue();
        metadata.Should().NotBeNull();
        metadata!.Name.Should().Be("test_tool");
    }

    [Fact]
    public void TryGetTool_WithNonExistentTool_ShouldReturnFalse()
    {
        var result = _registry.TryGetTool("nonexistent", out var metadata);
        result.Should().BeFalse();
        metadata.Should().BeNull();
    }

    [Fact]
    public void TryGetTool_BeforePluginDiscovery_ShouldReturnFalse()
    {
        var provider = CreateMockProvider("TestProvider", ["test_tool"]);
        _registry.RegisterProvider(provider.Object);

        var result = _registry.TryGetTool("test_tool", out var metadata);

        result.Should().BeFalse();
        metadata.Should().BeNull();
    }

    [Fact]
    public void GetProviderNames_ShouldReturnAllProviderNames()
    {
        var provider1 = CreateMockProvider("Provider1", ["tool1"]);
        var provider2 = CreateMockProvider("Provider2", ["tool2"]);
        _registry.RegisterProvider(provider1.Object);
        _registry.RegisterProvider(provider2.Object);

        var names = _registry.GetProviderNames();

        names.Should().HaveCount(2);
        names.Should().BeEquivalentTo(["Provider1", "Provider2"]);
    }

    #endregion

    #region 工具执行测试（渐进式路由）

    [Fact]
    public async Task ExecuteToolAsync_AfterRegistration_ShouldFindAndExecuteViaProvider()
    {
        var providerMock = CreateMockProvider("TestProvider", ["test_tool"]);
        providerMock.Setup(p => p.ExecuteAsync(
                "test_tool",
                It.IsAny<IReadOnlyDictionary<string, JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationResult
            {
                Success = true,
                ExitCode = 0,
                Message = "test output"
            });

        _registry.RegisterProvider(providerMock.Object);

        var result = await _registry.ExecuteToolAsync("test_tool", new Dictionary<string, JsonElement>());

        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.Message.Should().Be("test output");
    }

    [Fact]
    public async Task ExecuteToolAsync_WithNonExistentTool_ShouldReturnErrorResult()
    {
        var result = await _registry.ExecuteToolAsync("nonexistent", new Dictionary<string, JsonElement>());

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteToolAsync_WithCancellation_ShouldReturnCancelledResult()
    {
        var providerMock = CreateMockProvider("TestProvider", ["slow_tool"]);
        var cts = new CancellationTokenSource();

        providerMock.Setup(p => p.ExecuteAsync(
                "slow_tool",
                It.IsAny<IReadOnlyDictionary<string, JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string _, IReadOnlyDictionary<string, JsonElement> _, CancellationToken ct) =>
            {
                await Task.Delay(5000, ct);
                return new OperationResult { Success = true };
            });

        _registry.RegisterProvider(providerMock.Object);

        var executeTask = _registry.ExecuteToolAsync("slow_tool", new Dictionary<string, JsonElement>(), cts.Token);
        cts.CancelAfter(100);

        var result = await executeTask;

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("cancelled");
    }

    [Fact]
    public async Task ExecuteToolAsync_WithException_ShouldReturnErrorResult()
    {
        var providerMock = CreateMockProvider("TestProvider", ["error_tool"]);
        providerMock.Setup(p => p.ExecuteAsync(
                "error_tool",
                It.IsAny<IReadOnlyDictionary<string, JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        _registry.RegisterProvider(providerMock.Object);

        var result = await _registry.ExecuteToolAsync("error_tool", new Dictionary<string, JsonElement>());

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Test error");
    }

    #endregion

    #region 缓存集成测试（渐进式架构）

    [Fact]
    public async Task GetAllTools_ShouldUseCache_AfterFirstCall()
    {
        var provider = CreateMockProvider("TestProvider", ["tool1"]);
        _registry.RegisterProvider(provider.Object);

        await _registry.GetPluginCommandsAsync("TestProvider");
        var tools1 = _registry.GetAllTools();

        var tools2 = _registry.GetAllTools();

        tools1.Should().BeEquivalentTo(tools2);
        provider.Verify(p => p.GetAvailableTools(), Times.Once);
    }

    [Fact]
    public async Task TryGetTool_ShouldUseCache_AfterFirstCall()
    {
        var provider = CreateMockProvider("TestProvider", ["tool1"]);
        _registry.RegisterProvider(provider.Object);

        await _registry.GetPluginCommandsAsync("TestProvider");
        _registry.TryGetTool("tool1", out _);

        _registry.TryGetTool("tool1", out _);

        provider.Verify(p => p.GetAvailableTools(), Times.Once);
    }

    #endregion

    #region Helper Methods

    private static Mock<IToolProvider> CreateMockProvider(string name, string[] toolNames)
    {
        var mock = new Mock<IToolProvider>();
        mock.SetupGet(p => p.ProviderName).Returns(name);

        var tools = toolNames.Select(t =>
        {
            var toolMock = new Mock<IToolMetadata>();
            toolMock.SetupGet(m => m.Name).Returns(t);
            toolMock.SetupGet(m => m.Description).Returns($"Description for {t}");
            toolMock.SetupGet(m => m.Category).Returns("general");
            toolMock.SetupGet(m => m.InputSchema).Returns(System.Text.Json.JsonDocument.Parse("{}").RootElement);
            toolMock.SetupGet(m => m.DefaultTimeout).Returns(30000);
            toolMock.SetupGet(m => m.RequiredPermissions).Returns(Array.Empty<string>());
            return toolMock.Object;
        }).ToList();

        mock.Setup(p => p.GetAvailableTools()).Returns(tools);

        return mock;
    }

    #endregion
}
