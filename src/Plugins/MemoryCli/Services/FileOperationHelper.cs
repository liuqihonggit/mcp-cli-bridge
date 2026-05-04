using System.Text.Json;

namespace MemoryCli.Services;

internal static class FileOperationHelper
{
    public static void EnsureDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public static async Task AppendJsonLineAsync<T>(
        string filePath,
        T value,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken = default)
    {
        EnsureDirectory(filePath);
        var json = JsonSerializer.Serialize(value, jsonTypeInfo);
#pragma warning disable MCP001
        await File.AppendAllTextAsync(filePath, json + Environment.NewLine, cancellationToken).ConfigureAwait(false);
#pragma warning restore MCP001
    }

    public static async Task SaveJsonLinesAsync<T>(
        string filePath,
        IEnumerable<T> values,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken = default)
    {
        EnsureDirectory(filePath);
        var lines = values.Select(v => JsonSerializer.Serialize(v, jsonTypeInfo));
#pragma warning disable MCP001
        await File.WriteAllLinesAsync(filePath, lines, cancellationToken).ConfigureAwait(false);
#pragma warning restore MCP001
    }

    public static async Task<List<T>> ReadJsonLinesAsync<T>(
        string filePath,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

#pragma warning disable MCP001
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken).ConfigureAwait(false);
#pragma warning restore MCP001
        var result = new List<T>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var item = JsonSerializer.Deserialize(line, jsonTypeInfo);
                if (item != null)
                {
                    result.Add(item);
                }
            }
            catch
            {
                // 忽略解析失败的行
            }
        }

        return result;
    }
}
