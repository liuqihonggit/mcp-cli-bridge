namespace Service.Json.Contracts;

/// <summary>
/// 文件读取请求
/// </summary>
public class FileReaderRequest
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    [JsonPropertyName("lineCount")]
    public int LineCount { get; set; } = 10;
}

/// <summary>
/// 文件读取结果
/// </summary>
public class FileReadResult
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("lines")]
    public List<string> Lines { get; set; } = [];

    [JsonPropertyName("totalLines")]
    public int TotalLines { get; set; }

    [JsonPropertyName("requestedLines")]
    public int RequestedLines { get; set; }
}
