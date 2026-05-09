namespace McpHost.Services;

internal sealed class ConversationTracker
{
    private readonly ConcurrentQueue<ToolCallRecord> _records = new();
    private int _totalCount;

    internal sealed class ToolCallRecord
    {
        public string ToolName { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }

    public void Record(string toolName, string description)
    {
        _records.Enqueue(new ToolCallRecord
        {
            ToolName = toolName,
            Description = description
        });
        Interlocked.Increment(ref _totalCount);

        while (_records.Count > 100)
            _records.TryDequeue(out _);
    }

    public List<ToolCallRecord> GetRecent(int count)
    {
        return _records.TakeLast(count).ToList();
    }

    public int TotalCount => Volatile.Read(ref _totalCount);

    public void Clear()
    {
        while (_records.TryDequeue(out _)) { }
        Volatile.Write(ref _totalCount, 0);
    }
}
