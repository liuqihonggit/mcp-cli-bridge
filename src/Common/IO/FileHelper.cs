namespace Common.IO;

public static class FileHelper
{
    public static void EnsureDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public static async Task<T?> ReadJsonAsync<T>(
        string filePath,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken = default) where T : class
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        return System.Text.Json.JsonSerializer.Deserialize(json, jsonTypeInfo);
    }

    public static async Task WriteJsonAsync<T>(
        string filePath,
        T value,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken = default) where T : class
    {
        EnsureDirectory(filePath);
        var json = System.Text.Json.JsonSerializer.Serialize(value, jsonTypeInfo);
        await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
    }
}
