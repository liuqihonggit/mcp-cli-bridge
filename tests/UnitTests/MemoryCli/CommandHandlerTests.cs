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

    [Fact]
    public async Task SaveSummary_ShouldPersistAndGetRecent()
    {
        var saveRequest = new CliRequest
        {
            Command = "save_summary",
            Title = "讨论项目架构",
            UserMessages = ["我想用微服务架构", "数据库选型用PostgreSQL"]
        };
        var saveResult = await _handler.ExecuteAsync(saveRequest);
        saveResult.Success.Should().BeTrue();

        var getRequest = new CliRequest
        {
            Command = "get_recent_summaries",
            Limit = 10
        };
        var getResult = await _handler.ExecuteAsync(getRequest);
        getResult.Success.Should().BeTrue();

        var summaries = getResult.Data.Deserialize(CommonJsonContext.Default.ConversationSummaryList);
        summaries!.Summaries.Should().HaveCount(1);
        summaries.Summaries[0].Title.Should().Be("讨论项目架构");
        summaries.Summaries[0].UserMessages.Should().Contain("我想用微服务架构");
    }

    [Fact]
    public async Task GetRecentSummaries_ShouldReturnLimitedResults()
    {
        for (int i = 0; i < 5; i++)
        {
            var saveRequest = new CliRequest
            {
                Command = "save_summary",
                Title = $"对话{i}",
                UserMessages = [$"用户消息{i}"]
            };
            await _handler.ExecuteAsync(saveRequest);
        }

        var getRequest = new CliRequest
        {
            Command = "get_recent_summaries",
            Limit = 3
        };
        var getResult = await _handler.ExecuteAsync(getRequest);
        getResult.Success.Should().BeTrue();

        var summaries = getResult.Data.Deserialize(CommonJsonContext.Default.ConversationSummaryList);
        summaries!.Summaries.Should().HaveCount(3);
        summaries.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task SaveSummary_EmptyTitle_ShouldFail()
    {
        var saveRequest = new CliRequest
        {
            Command = "save_summary",
            Title = "",
            UserMessages = ["test"]
        };
        var result = await _handler.ExecuteAsync(saveRequest);
        result.Success.Should().BeFalse();
    }
}
