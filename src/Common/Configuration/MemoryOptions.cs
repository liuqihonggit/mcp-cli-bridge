namespace Common.Configuration;

public sealed class MemoryOptions : OptionsBase<MemoryOptions>
{
    private const string MemoryPathEnvVar = "MCP_MEMORY_PATH";

    public string BaseDirectory { get; set; }

    public string MemoryFileName { get; set; } = $"{FileNames.Memory}{FileExtensions.Jsonl}";

    public string RelationsFileName { get; set; } = $"{FileNames.Relations}{FileExtensions.Jsonl}";

    public TimeSpan LockTimeout { get; set; } = Timeouts.DefaultLock;

    public MemoryOptions()
    {
        BaseDirectory = ResolveBaseDirectory();
    }

    public MemoryOptions(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        BaseDirectory = Path.GetFullPath(baseDirectory);
    }

    private static string ResolveBaseDirectory()
    {
        var envPath = Environment.GetEnvironmentVariable(MemoryPathEnvVar);
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(envPath));
        }

        throw new InvalidOperationException(
            $"未设置记忆存储路径。请通过以下任一方式配置:{Environment.NewLine}" +
            $"  1. 设置环境变量 {MemoryPathEnvVar}{Environment.NewLine}" +
            $"  2. 或通过构造函数传入 baseDirectory 参数");
    }

    public string GetMemoryPath() => Path.Combine(BaseDirectory, MemoryFileName);

    public string GetRelationsPath() => Path.Combine(BaseDirectory, RelationsFileName);
}
