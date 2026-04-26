using Common.Contracts;

namespace MyMemoryServer.UnitTests.Plugins;

/// <summary>
/// 插件架构单元测试 - 测试工具注册、发现、执行
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

    #region 工具提供者注册测试

    [Fact]
    public void RegisterProvider_ShouldRegisterToolsSuccessfully()
    {
        // Arrange
        var providerMock = CreateMockProvider("TestProvider", ["tool1", "tool2"]);

        // Act
        _registry.RegisterProvider(providerMock.Object);

        // Assert
        _registry.ProviderCount.Should().Be(1);
        _registry.ToolCount.Should().Be(2);
    }

    [Fact]
    public void RegisterProvider_WithDuplicateName_ShouldSkipRegistration()
    {
        // Arrange
        var provider1 = CreateMockProvider("TestProvider", ["tool1"]);
        var provider2 = CreateMockProvider("TestProvider", ["tool2"]);

        // Act
        _registry.RegisterProvider(provider1.Object);
        _registry.RegisterProvider(provider2.Object);

        // Assert
        _registry.ProviderCount.Should().Be(1);
        _registry.ToolCount.Should().Be(1);
    }

    [Fact]
    public void RegisterProvider_WithConflictingToolNames_ShouldSkipConflictingTools()
    {
        // Arrange
        var provider1 = CreateMockProvider("Provider1", ["tool1", "tool2"]);
        var provider2 = CreateMockProvider("Provider2", ["tool2", "tool3"]);

        // Act
        _registry.RegisterProvider(provider1.Object);
        _registry.RegisterProvider(provider2.Object);

        // Assert
        _registry.ProviderCount.Should().Be(2);
        _registry.ToolCount.Should().Be(3); // tool1, tool2 (from provider1), tool3
    }

    [Fact]
    public void RegisterProvider_WithNullProvider_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _registry.RegisterProvider(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region 工具提供者注销测试

    [Fact]
    public void UnregisterProvider_ShouldRemoveAllProviderTools()
    {
        // Arrange
        var provider = CreateMockProvider("TestProvider", ["tool1", "tool2"]);
        _registry.RegisterProvider(provider.Object);

        // Act
        var result = _registry.UnregisterProvider("TestProvider");

        // Assert
        result.Should().BeTrue();
        _registry.ProviderCount.Should().Be(0);
        _registry.ToolCount.Should().Be(0);
    }

    [Fact]
    public void UnregisterProvider_WithNonExistentName_ShouldReturnFalse()
    {
        // Act
        var result = _registry.UnregisterProvider("NonExistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void UnregisterProvider_WithNullOrEmptyName_ShouldThrowArgumentException()
    {
        // Act
        var act1 = () => _registry.UnregisterProvider(null!);
        var act2 = () => _registry.UnregisterProvider(string.Empty);
        var act3 = () => _registry.UnregisterProvider("   ");

        // Assert
        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
        act3.Should().Throw<ArgumentException>();
    }

    #endregion

    #region 工具发现测试

    [Fact]
    public void GetAllTools_ShouldReturnAllRegisteredTools()
    {
        // Arrange
        var provider1 = CreateMockProvider("Provider1", ["tool1", "tool2"]);
        var provider2 = CreateMockProvider("Provider2", ["tool3"]);
        _registry.RegisterProvider(provider1.Object);
        _registry.RegisterProvider(provider2.Object);

        // Act
        var tools = _registry.GetAllTools();

        // Assert
        tools.Should().HaveCount(3);
        tools.Select(t => t.Name).Should().BeEquivalentTo(["tool1", "tool2", "tool3"]);
    }

    [Fact]
    public void GetAllTools_WithNoProviders_ShouldReturnEmptyList()
    {
        // Act
        var tools = _registry.GetAllTools();

        // Assert
        tools.Should().BeEmpty();
    }

    [Fact]
    public void TryGetTool_WithExistingTool_ShouldReturnTrue()
    {
        // Arrange
        var provider = CreateMockProvider("TestProvider", ["test_tool"]);
        _registry.RegisterProvider(provider.Object);

        // Act
        var result = _registry.TryGetTool("test_tool", out var metadata);

        // Assert
        result.Should().BeTrue();
        metadata.Should().NotBeNull();
        metadata!.Name.Should().Be("test_tool");
    }

    [Fact]
    public void TryGetTool_WithNonExistentTool_ShouldReturnFalse()
    {
        // Act
        var result = _registry.TryGetTool("nonexistent", out var metadata);

        // Assert
        result.Should().BeFalse();
        metadata.Should().BeNull();
    }

    [Fact]
    public void GetProviderNames_ShouldReturnAllProviderNames()
    {
        // Arrange
        var provider1 = CreateMockProvider("Provider1", ["tool1"]);
        var provider2 = CreateMockProvider("Provider2", ["tool2"]);
        _registry.RegisterProvider(provider1.Object);
        _registry.RegisterProvider(provider2.Object);

        // Act
        var names = _registry.GetProviderNames();

        // Assert
        names.Should().HaveCount(2);
        names.Should().BeEquivalentTo(["Provider1", "Provider2"]);
    }

    #endregion

    #region 工具执行测试

    [Fact]
    public async Task ExecuteToolAsync_WithValidTool_ShouldReturnSuccessResult()
    {
        // Arrange
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

        // Act
        var result = await _registry.ExecuteToolAsync("test_tool", new Dictionary<string, JsonElement>());

        // Assert
        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.Message.Should().Be("test output");
    }

    [Fact]
    public async Task ExecuteToolAsync_WithNonExistentTool_ShouldReturnErrorResult()
    {
        // Act
        var result = await _registry.ExecuteToolAsync("nonexistent", new Dictionary<string, JsonElement>());

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteToolAsync_WithCancellation_ShouldReturnCancelledResult()
    {
        // Arrange
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

        // Act
        var executeTask = _registry.ExecuteToolAsync("slow_tool", new Dictionary<string, JsonElement>(), cts.Token);
        cts.CancelAfter(100);

        var result = await executeTask;

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("cancelled");
    }

    [Fact]
    public async Task ExecuteToolAsync_WithException_ShouldReturnErrorResult()
    {
        // Arrange
        var providerMock = CreateMockProvider("TestProvider", ["error_tool"]);
        providerMock.Setup(p => p.ExecuteAsync(
                "error_tool",
                It.IsAny<IReadOnlyDictionary<string, JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        _registry.RegisterProvider(providerMock.Object);

        // Act
        var result = await _registry.ExecuteToolAsync("error_tool", new Dictionary<string, JsonElement>());

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Test error");
    }

    #endregion

    #region 缓存集成测试

    [Fact]
    public void GetAllTools_ShouldUseCache_WhenCacheIsAvailable()
    {
        // Arrange
        var provider = CreateMockProvider("TestProvider", ["tool1"]);
        _registry.RegisterProvider(provider.Object);

        // First call - should populate cache
        var tools1 = _registry.GetAllTools();

        // Act - Second call should use cache
        var tools2 = _registry.GetAllTools();

        // Assert
        tools1.Should().BeEquivalentTo(tools2);
        provider.Verify(p => p.GetAvailableTools(), Times.Once);
    }

    [Fact]
    public void TryGetTool_ShouldUseCache_WhenCacheIsAvailable()
    {
        // Arrange
        var provider = CreateMockProvider("TestProvider", ["tool1"]);
        _registry.RegisterProvider(provider.Object);

        // First call - should populate cache
        _registry.TryGetTool("tool1", out _);

        // Act - Second call should use cache
        _registry.TryGetTool("tool1", out _);

        // Assert
        provider.Verify(p => p.GetAvailableTools(), Times.Once);
    }

    #endregion

    #region Helper Methods

    private static Mock<IToolProvider> CreateMockProvider(string name, string[] toolNames)
    {
        var mock = new Mock<IToolProvider>();
        mock.SetupGet(p => p.ProviderName).Returns(name);

        var tools = toolNames.Select(t => new Mock<IToolMetadata>().Object).ToList();

        // Setup each mock tool
        for (int i = 0; i < toolNames.Length; i++)
        {
            var toolMock = new Mock<IToolMetadata>();
            toolMock.SetupGet(m => m.Name).Returns(toolNames[i]);
            toolMock.SetupGet(m => m.Description).Returns($"Description for {toolNames[i]}");
            tools[i] = toolMock.Object;
        }

        mock.Setup(p => p.GetAvailableTools()).Returns(tools);

        return mock;
    }

    #endregion
}
