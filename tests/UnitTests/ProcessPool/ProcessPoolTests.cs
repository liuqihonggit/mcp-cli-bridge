namespace MyMemoryServer.UnitTests.ProcessPool;

public sealed class ProcessPoolTests : IDisposable
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly ProcessPoolManager _poolManager;

    public ProcessPoolTests()
    {
        _loggerMock = new Mock<ILogger>();
        _poolManager = new ProcessPoolManager(_loggerMock.Object);
    }

    public void Dispose()
    {
        (_poolManager as IDisposable)?.Dispose();
    }

    [Fact]
    public void Constructor_WithValidOptions_ShouldCreateInstance()
    {
        // Act & Assert
        _poolManager.Should().NotBeNull();
    }

    [Fact]
    public void GetOrCreatePool_WithNewName_ShouldCreateNewPool()
    {
        // Arrange
        var options = new ProcessPoolOptions
        {
            MaxPoolSize = 2
        };

        // Act
        var pool = _poolManager.GetOrCreatePool("test_pool", "dotnet", options);

        // Assert
        pool.Should().NotBeNull();
        _poolManager.GetPoolNames().Should().Contain("test_pool");
    }

    [Fact]
    public void GetOrCreatePool_WithExistingName_ShouldReturnSamePool()
    {
        // Arrange
        var options = new ProcessPoolOptions
        {
            MaxPoolSize = 2
        };

        // Act
        var pool1 = _poolManager.GetOrCreatePool("existing_pool", "dotnet", options);
        var pool2 = _poolManager.GetOrCreatePool("existing_pool", "dotnet", options);

        // Assert
        pool1.Should().BeSameAs(pool2);
    }

    [Fact]
    public async Task RemovePoolAsync_WithExistingPool_ShouldRemoveAndDispose()
    {
        // Arrange
        var options = new ProcessPoolOptions
        {
            MaxPoolSize = 1
        };

        _poolManager.GetOrCreatePool("removable_pool", "dotnet", options);

        // Act
        await _poolManager.RemovePoolAsync("removable_pool");

        // Assert
        _poolManager.GetPoolNames().Should().NotContain("removable_pool");
    }

    [Fact]
    public async Task RemovePoolAsync_WithNonExistingPool_ShouldNotThrow()
    {
        // Act
        await _poolManager.RemovePoolAsync("nonexistent_pool");

        // Assert - should not throw
        _poolManager.GetPoolNames().Should().BeEmpty();
    }
}
