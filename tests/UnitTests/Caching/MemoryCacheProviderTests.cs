namespace MyMemoryServer.UnitTests.Caching;

public sealed class MemoryCacheProviderTests : IDisposable
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly MemoryCacheProvider _cache;

    public MemoryCacheProviderTests()
    {
        _loggerMock = new Mock<ILogger>();
        _cache = new MemoryCacheProvider(_loggerMock.Object, new MemoryCacheOptions
        {
            MaxEntries = 100,
            DefaultExpiration = TimeSpan.FromMinutes(5)
        });
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    [Fact]
    public void Set_WithNewKey_ShouldAddEntry()
    {
        // Arrange
        var key = "test_key";
        var value = "test_value";

        // Act
        _cache.Set(key, value);
        var result = _cache.TryGet<string>(key, out var retrievedValue);

        // Assert
        result.Should().BeTrue();
        retrievedValue.Should().Be(value);
    }

    [Fact]
    public void TryGet_WithNonExistingKey_ShouldReturnFalse()
    {
        // Act
        var result = _cache.TryGet<string>("nonexistent_key", out var value);

        // Assert
        result.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void Remove_WithExistingKey_ShouldRemoveEntry()
    {
        // Arrange
        var key = "removable_key";
        _cache.Set(key, "value");

        // Act
        var removed = _cache.Remove(key);
        var exists = _cache.TryGet<string>(key, out _);

        // Assert
        removed.Should().BeTrue();
        exists.Should().BeFalse();
    }

    [Fact]
    public void GetOrCreate_WithNewKey_ShouldCreateAndReturnValue()
    {
        // Arrange
        var key = "factory_key";
        var value = "factory_value";

        // Act
        var result = _cache.GetOrCreate(key, () => value);

        // Assert
        result.Should().Be(value);
        _cache.TryGet<string>(key, out var cachedValue).Should().BeTrue();
        cachedValue.Should().Be(value);
    }

    [Fact]
    public void GetOrCreate_WithExistingKey_ShouldReturnCachedValue()
    {
        // Arrange
        var key = "existing_factory_key";
        var originalValue = "original_value";
        var newValue = "new_value";
        _cache.Set(key, originalValue);

        // Act
        var result = _cache.GetOrCreate(key, () => newValue);

        // Assert
        result.Should().Be(originalValue);
    }
}
