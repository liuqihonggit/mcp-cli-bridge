using MemoryCli.Services;
using Common.Configuration;

namespace MyMemoryServer.UnitTests.StorageProvider;

public sealed class StoreProviderRegistryTests
{
    [Fact]
    public void GetStore_JsonlByName_ReturnsSupportedProvider()
    {
        var provider = StoreProviderRegistry.GetProvider("jsonl");
        provider.Backend.IsSupported.Should().BeTrue();
        provider.Backend.Name.Should().Be("jsonl");
    }

    [Fact]
    public void GetStore_NullOrDefault_ReturnsJsonlProvider()
    {
        var nullProvider = StoreProviderRegistry.GetProvider(null);
        var emptyProvider = StoreProviderRegistry.GetProvider("");

        nullProvider.Backend.IsSupported.Should().BeTrue();
        emptyProvider.Backend.IsSupported.Should().BeTrue();
    }

    [Fact]
    public void GetStore_Sqlite_ReturnsUnsupportedProvider()
    {
        var provider = StoreProviderRegistry.GetProvider("sqlite");
        provider.Backend.IsSupported.Should().BeFalse();
        provider.Backend.Name.Should().Be("sqlite");
    }

    [Fact]
    public void GetStore_Redis_ReturnsUnsupportedProvider()
    {
        var provider = StoreProviderRegistry.GetProvider("redis");
        provider.Backend.IsSupported.Should().BeFalse();
    }

    [Fact]
    public void GetStore_CaseInsensitive_ReturnsCorrectProvider()
    {
        var upperProvider = StoreProviderRegistry.GetProvider("JSONL");
        upperProvider.Backend.IsSupported.Should().BeTrue();
    }

    [Fact]
    public void GetSupportedBackends_ContainsJsonl()
    {
        var supported = StoreProviderRegistry.GetSupportedBackends();
        supported.Should().Contain("jsonl");
    }

    [Fact]
    public void Register_NewBackend_ReturnsProvider()
    {
        StoreProviderRegistry.Register("leveldb", static () => new TestLevelDbStoreProvider());
        var provider = StoreProviderRegistry.GetProvider("leveldb");
        provider.Backend.IsSupported.Should().BeTrue();
        provider.Backend.Name.Should().Be("leveldb");
        StoreProviderRegistry.Unregister("leveldb");
    }

    private sealed class TestLevelDbStoreProvider : IStoreProvider
    {
        public StoreBackendInfo Backend => new() { Name = "leveldb", DisplayName = "LevelDB", IsSupported = true };
        public IKnowledgeGraphStore CreateStore(MemoryOptions options) => throw new NotImplementedException();
    }
}
