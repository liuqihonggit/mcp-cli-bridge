using System.Buffers;
using System.Text.Json.Serialization.Metadata;

namespace FileLock;

public static class FileOperationHelper
{
    public const int DefaultMaxRetries = 3;
    public const int DefaultBaseDelayMs = 100;
    public const int DefaultBufferSize = 8192;

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
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) where T : class
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(bytes.AsSpan(), typeInfo);
    }

    public static async Task WriteJsonAsync<T>(
        string filePath,
        T data,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default)
    {
        EnsureDirectory(filePath);

        var bufferWriter = new ArrayBufferWriter<byte>(DefaultBufferSize);
        using var writer = new Utf8JsonWriter(bufferWriter);

        JsonSerializer.Serialize(writer, data, typeInfo);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

        await WriteBytesWithRetryAsync(filePath, bufferWriter.WrittenMemory, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<List<T>> ReadJsonLinesAsync<T>(
        string filePath,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) where T : class
    {
        var items = new List<T>();

        if (!File.Exists(filePath))
        {
            return items;
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            DefaultBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, DefaultBufferSize);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var lineBytes = System.Text.Encoding.UTF8.GetBytes(line);
                var item = JsonSerializer.Deserialize(lineBytes.AsSpan(), typeInfo);
                if (item is not null)
                {
                    items.Add(item);
                }
            }
            catch
            {
            }
        }

        return items;
    }

    public static async Task AppendJsonLineAsync<T>(
        string filePath,
        T data,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default)
    {
        EnsureDirectory(filePath);

        var bufferWriter = new ArrayBufferWriter<byte>(DefaultBufferSize);
        using var writer = new Utf8JsonWriter(bufferWriter);

        JsonSerializer.Serialize(writer, data, typeInfo);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

        bufferWriter.Write(System.Text.Encoding.UTF8.GetBytes(Environment.NewLine));

        await AppendBytesWithRetryAsync(filePath, bufferWriter.WrittenMemory, cancellationToken).ConfigureAwait(false);
    }

    public static async Task SaveJsonLinesAsync<T>(
        string filePath,
        IReadOnlyList<T> items,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default)
    {
        EnsureDirectory(filePath);

        var bufferWriter = new ArrayBufferWriter<byte>(DefaultBufferSize * items.Count);

        for (int i = 0; i < items.Count; i++)
        {
            using var writer = new Utf8JsonWriter(bufferWriter);
            JsonSerializer.Serialize(writer, items[i], typeInfo);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

            if (i < items.Count - 1)
            {
                bufferWriter.Write(System.Text.Encoding.UTF8.GetBytes(Environment.NewLine));
            }
        }

        await WriteBytesWithRetryAsync(filePath, bufferWriter.WrittenMemory, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteBytesWithRetryAsync(
        string filePath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken,
        int maxRetries = DefaultMaxRetries,
        int baseDelayMs = DefaultBaseDelayMs)
    {
        for (int retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                await WriteBytesCowAsync(filePath, content, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (IOException) when (retry < maxRetries - 1)
            {
                await Task.Delay(baseDelayMs * (retry + 1), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task WriteBytesCowAsync(
        string filePath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(filePath);
        var tmpPath = Path.Combine(dir ?? ".", Path.GetRandomFileName());

        try
        {
            await using (var stream = new FileStream(
                tmpPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                DefaultBufferSize,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            for (int retry = 0; retry < DefaultMaxRetries; retry++)
            {
                try
                {
                    File.Move(tmpPath, filePath, overwrite: true);
                    return;
                }
                catch (IOException) when (retry < DefaultMaxRetries - 1)
                {
                    Thread.Sleep(DefaultBaseDelayMs);
                }
            }

            File.Move(tmpPath, filePath, overwrite: true);
        }
        catch
        {
            try { File.Delete(tmpPath); } catch { }
            throw;
        }
    }

    private static async Task AppendBytesWithRetryAsync(
        string filePath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken,
        int maxRetries = DefaultMaxRetries,
        int baseDelayMs = DefaultBaseDelayMs)
    {
        for (int retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                ReadOnlyMemory<byte> combined;

                if (File.Exists(filePath))
                {
                    var existing = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
                    var result = new byte[existing.Length + content.Length];
                    Array.Copy(existing, 0, result, 0, existing.Length);
                    content.Span.CopyTo(result.AsSpan(existing.Length));
                    combined = result;
                }
                else
                {
                    combined = content;
                }

                await WriteBytesCowAsync(filePath, combined, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (IOException) when (retry < maxRetries - 1)
            {
                await Task.Delay(baseDelayMs * (retry + 1), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public static bool SafeDelete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    public static long GetFileSize(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return 0;
        }

        try
        {
            return new FileInfo(filePath).Length;
        }
        catch
        {
            return 0;
        }
    }

    public static string GenerateTimestampedFileName(string baseName, string extension)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"{baseName}_{timestamp}{extension}";
    }

    public static async Task<bool> SafeMoveAsync(
        string sourcePath,
        string destPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
        {
            return false;
        }

        EnsureDirectory(destPath);

        for (int retry = 0; retry < DefaultMaxRetries; retry++)
        {
            try
            {
                File.Move(sourcePath, destPath, overwrite: true);
                return true;
            }
            catch (IOException) when (retry < DefaultMaxRetries - 1)
            {
                await Task.Delay(DefaultBaseDelayMs * (retry + 1), cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    public static async Task<bool> CopyAsync(
        string sourcePath,
        string destPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
        {
            return false;
        }

        EnsureDirectory(destPath);

        for (int retry = 0; retry < DefaultMaxRetries; retry++)
        {
            try
            {
                await using var sourceStream = new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    DefaultBufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                await using var destStream = new FileStream(
                    destPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    DefaultBufferSize,
                    FileOptions.Asynchronous);

                await sourceStream.CopyToAsync(destStream, cancellationToken).ConfigureAwait(false);
                await destStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (IOException) when (retry < DefaultMaxRetries - 1)
            {
                await Task.Delay(DefaultBaseDelayMs * (retry + 1), cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    public static bool IsFileLocked(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    public static async Task<bool> WaitForFileUnlockAsync(
        string filePath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            if (!IsFileLocked(filePath))
            {
                return true;
            }

            await Task.Delay(DefaultBaseDelayMs, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    public static async Task WriteTextAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        EnsureDirectory(filePath);

        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        await WriteBytesWithRetryAsync(filePath, bytes, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<string?> ReadTextAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
    }
}
