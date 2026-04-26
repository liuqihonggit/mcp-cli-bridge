namespace Common.Configuration;

public sealed class MemoryOptions
{
    private const string MemoryPathEnvVar = "MCP_MEMORY_PATH";

    public string BaseDirectory { get; set; }

    public string MemoryFileName { get; set; } = $"{FileNames.Memory}{FileExtensions.Jsonl}";

    public string RelationsFileName { get; set; } = $"{FileNames.Relations}{FileExtensions.Jsonl}";

    public TimeSpan LockTimeout { get; set; } = Timeouts.DefaultLock;

    public MemoryOptions()
    {
        BaseDirectory = GetBaseDirectoryFromEnvironment()
            ?? Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? DefaultPaths.DriveC, DefaultPaths.McpDirectory, DefaultPaths.MemoryDirectory);
    }

    private static string? GetBaseDirectoryFromEnvironment()
    {
        var envPath = Environment.GetEnvironmentVariable(MemoryPathEnvVar);
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return envPath;
        }
        return null;
    }

    public string GetMemoryPath() => Path.Combine(BaseDirectory, MemoryFileName);

    public string GetRelationsPath() => Path.Combine(BaseDirectory, RelationsFileName);
}
