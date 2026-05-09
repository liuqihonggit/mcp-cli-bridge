using Common.Configuration;
using Common.Contracts.Models;
using Common.Json;
using MemoryCli.Commands;
using MemoryCli.Services;

namespace MyMemoryServer.UnitTests.MemoryCli;

public sealed class CommandHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MemoryOptions _options;
    private readonly IKnowledgeGraphStore _store;
    private readonly CommandHandler _handler;

    public CommandHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"MemoryCli_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _options = new MemoryOptions(_tempDir);
        var storeProvider = StoreProviderRegistry.GetProvider(null);
        _store = storeProvider.CreateStore(_options);
        _handler = new CommandHandler(_store, _options);
    }

    public void Dispose()
    {
        _store.Dispose();
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch { }
    }

    [Fact]
    public async Task CreateEntities_SameName_ShouldOverwriteExisting()
    {
        var createRequest1 = new CliRequest
        {
            Command = "create_entities",
            Entities =
            [
                new KnowledgeGraphEntity { Name = "Alice", EntityType = "person", Observations = ["likes coffee"] }
            ]
        };
        await _handler.ExecuteAsync(createRequest1);

        var createRequest2 = new CliRequest
        {
            Command = "create_entities",
            Entities =
            [
                new KnowledgeGraphEntity { Name = "Alice", EntityType = "person", Observations = ["likes tea"] }
            ]
        };
        var result = await _handler.ExecuteAsync(createRequest2);

        result.Success.Should().BeTrue();
        var countResult = result.Data.Deserialize(CommonJsonContext.Default.CountResult);
        countResult!.Updated.Should().Be(1);
        countResult.Count.Should().Be(0);

        var readResult = await _handler.ExecuteAsync(new CliRequest { Command = "read_graph" });
        readResult.Success.Should().BeTrue();
        var graph = readResult.Data.Deserialize(CommonJsonContext.Default.KnowledgeGraphData);
        graph!.Entities.Should().HaveCount(1);
        graph.Entities[0].Observations.Should().Contain("likes tea");
        graph.Entities[0].Observations.Should().NotContain("likes coffee");
    }

    [Fact]
    public async Task CreateEntities_NewName_ShouldAddAsBefore()
    {
        var request = new CliRequest
        {
            Command = "create_entities",
            Entities =
            [
                new KnowledgeGraphEntity { Name = "Bob", EntityType = "person", Observations = ["developer"] }
            ]
        };
        var result = await _handler.ExecuteAsync(request);

        result.Success.Should().BeTrue();
        var countResult = result.Data.Deserialize(CommonJsonContext.Default.CountResult);
        countResult!.Count.Should().Be(1);
        countResult.Updated.Should().Be(0);
    }

    [Fact]
    public async Task CreateEntities_MixedNewAndExisting_ShouldReportBothCounts()
    {
        var createRequest1 = new CliRequest
        {
            Command = "create_entities",
            Entities =
            [
                new KnowledgeGraphEntity { Name = "Alice", EntityType = "person", Observations = ["old"] }
            ]
        };
        await _handler.ExecuteAsync(createRequest1);

        var createRequest2 = new CliRequest
        {
            Command = "create_entities",
            Entities =
            [
                new KnowledgeGraphEntity { Name = "Alice", EntityType = "person", Observations = ["new"] },
                new KnowledgeGraphEntity { Name = "Bob", EntityType = "person", Observations = ["developer"] }
            ]
        };
        var result = await _handler.ExecuteAsync(createRequest2);

        result.Success.Should().BeTrue();
        var countResult = result.Data.Deserialize(CommonJsonContext.Default.CountResult);
        countResult!.Count.Should().Be(1);
        countResult.Updated.Should().Be(1);
    }
}
