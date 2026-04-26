namespace McpHost.ProcessPool;

/// <summary>
/// 进程池配置选项
/// </summary>
public sealed class ProcessPoolOptions
{
    /// <summary>
    /// 进程池最大容量
    /// </summary>
    [JsonPropertyName("maxPoolSize")]
    public int MaxPoolSize { get; init; } = 5;

    /// <summary>
    /// 进程空闲超时时间（超过此时间未使用则回收）
    /// </summary>
    [JsonPropertyName("idleTimeout")]
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 获取进程的超时时间
    /// </summary>
    [JsonPropertyName("acquireTimeout")]
    public TimeSpan AcquireTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 进程启动超时时间
    /// </summary>
    [JsonPropertyName("startupTimeout")]
    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 健康检查间隔
    /// </summary>
    [JsonPropertyName("healthCheckInterval")]
    public TimeSpan HealthCheckInterval { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 进程最大使用次数（超过后回收，0表示不限制）
    /// </summary>
    [JsonPropertyName("maxUsageCount")]
    public int MaxUsageCount { get; init; } = 0;

    /// <summary>
    /// 进程最大生命周期（超过后回收，0表示不限制）
    /// </summary>
    [JsonPropertyName("maxLifetime")]
    public TimeSpan MaxLifetime { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// 是否启用进程复用
    /// </summary>
    [JsonPropertyName("enableReuse")]
    public bool EnableReuse { get; init; } = true;

    /// <summary>
    /// 进程启动参数
    /// </summary>
    [JsonPropertyName("startupArguments")]
    public string? StartupArguments { get; init; }

    /// <summary>
    /// 工作目录
    /// </summary>
    [JsonPropertyName("workingDirectory")]
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// 环境变量
    /// </summary>
    [JsonPropertyName("environmentVariables")]
    public Dictionary<string, string>? EnvironmentVariables { get; init; }

    /// <summary>
    /// 创建默认配置
    /// </summary>
    public static ProcessPoolOptions Default => new();

    /// <summary>
    /// 从配置值创建选项
    /// </summary>
    public static ProcessPoolOptions FromConfiguration(int poolSize, string timeout)
    {
        var timeoutValue = TimeSpan.TryParse(timeout, out var parsed)
            ? parsed
            : TimeSpan.FromSeconds(30);

        return new ProcessPoolOptions
        {
            MaxPoolSize = poolSize > 0 ? poolSize : 5,
            AcquireTimeout = timeoutValue,
            StartupTimeout = TimeSpan.FromSeconds(10),
            IdleTimeout = TimeSpan.FromMinutes(5),
            HealthCheckInterval = TimeSpan.FromMinutes(1)
        };
    }
}
