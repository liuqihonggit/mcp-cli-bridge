using McpHost.Services;

namespace MyMemoryServer.UnitTests.McpHost;

public sealed class ConversationTrackerTests
{
    [Fact]
    public void Record_ShouldIncrementTotalCount()
    {
        var tracker = new ConversationTracker();
        tracker.Record("test_tool", "test description");

        tracker.TotalCount.Should().Be(1);
    }

    [Fact]
    public void Record_MultipleRecords_ShouldTrackAll()
    {
        var tracker = new ConversationTracker();
        tracker.Record("tool_a", "desc a");
        tracker.Record("tool_b", "desc b");
        tracker.Record("tool_c", "desc c");

        tracker.TotalCount.Should().Be(3);
        var recent = tracker.GetRecent(3);
        recent.Count.Should().Be(3);
        recent[0].ToolName.Should().Be("tool_a");
        recent[2].ToolName.Should().Be("tool_c");
    }

    [Fact]
    public void GetRecent_ShouldReturnLimitedResults()
    {
        var tracker = new ConversationTracker();
        for (int i = 0; i < 10; i++)
            tracker.Record($"tool_{i}", $"desc_{i}");

        var recent = tracker.GetRecent(3);
        recent.Count.Should().Be(3);
        recent[0].ToolName.Should().Be("tool_7");
        recent[2].ToolName.Should().Be("tool_9");
    }

    [Fact]
    public void GetRecent_MoreThanAvailable_ShouldReturnAll()
    {
        var tracker = new ConversationTracker();
        tracker.Record("tool_1", "desc_1");
        tracker.Record("tool_2", "desc_2");

        var recent = tracker.GetRecent(10);
        recent.Count.Should().Be(2);
    }

    [Fact]
    public void Clear_ShouldResetAllState()
    {
        var tracker = new ConversationTracker();
        tracker.Record("tool_1", "desc_1");
        tracker.Record("tool_2", "desc_2");

        tracker.Clear();

        tracker.TotalCount.Should().Be(0);
        tracker.GetRecent(10).Should().BeEmpty();
    }

    [Fact]
    public void Record_ExceedMaxCapacity_ShouldEvictOldest()
    {
        var tracker = new ConversationTracker();
        for (int i = 0; i < 110; i++)
            tracker.Record($"tool_{i}", $"desc_{i}");

        tracker.TotalCount.Should().Be(110);
        var recent = tracker.GetRecent(110);
        recent.Count.Should().Be(100);
        recent[0].ToolName.Should().Be("tool_10");
    }
}
