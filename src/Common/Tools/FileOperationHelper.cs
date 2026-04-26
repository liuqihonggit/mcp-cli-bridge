using System.Buffers;
using System.Text.Json.Serialization.Metadata;

namespace Common.Tools;

/// <summary>
/// 文件操作帮助类，提供统一的文件读写操作
/// </summary>
public static class FileOperationHelper
{
    /// <summary>
    /// 默认重试次数
    /// </summary>
    public const int DefaultMaxRetries = 3;

    /// <summary>
    /// 默认基础延迟（毫秒）
    /// </summary>
    public const int DefaultBaseDelayMs = 100;

    /// <summary>
    /// 默认缓冲区大小（8KB）
    /// </summary>
    public const int DefaultBufferSize = 8192;

    /// <summary>
    /// 确保目录存在
    /// </summary>
    /// <param name="filePath">文件路径</param>
    public static void EnsureDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// 异步读取 JSON 文件
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="typeInfo">JSON 类型信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化后的数据，如果文件不存在则返回默认值</returns>
    public static async Task<T?> ReadJsonAsync<T>(
        string filePath,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) where T : class
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        // 使用 Span<T> 优化：直接读取字节并使用 UTF8 反序列化
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        return JsonSerializer.Deserialize(bytes.AsSpan(), typeInfo);
    }

    /// <summary>
    /// 异步写入 JSON 文件（带重试逻辑）
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="data">数据</param>
    /// <param name="typeInfo">JSON 类型信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task WriteJsonAsync<T>(
        string filePath,
        T data,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default)
    {
        EnsureDirectory(filePath);

        // 使用 ArrayPool 减少内存分配
        var bufferWriter = new ArrayBufferWriter<byte>(DefaultBufferSize);
        using var writer = new Utf8JsonWriter(bufferWriter);

        JsonSerializer.Serialize(writer, data, typeInfo);
        await writer.FlushAsync(cancellationToken);

        await WriteBytesWithRetryAsync(filePath, bufferWriter.WrittenMemory, cancellationToken);
    }

    /// <summary>
    /// 异步读取 JSON Lines 文件
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="typeInfo">JSON 类型信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据列表</returns>
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

        // 使用 FileStream 和 StreamReader 实现真正的异步流式读取
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            DefaultBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, DefaultBufferSize);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                // 使用 Span<T> 优化：将字符串转换为 UTF8 字节进行反序列化
                var lineBytes = System.Text.Encoding.UTF8.GetBytes(line);
                var item = JsonSerializer.Deserialize(lineBytes.AsSpan(), typeInfo);
                if (item is not null)
                {
                    items.Add(item);
                }
            }
            catch
            {
                // 忽略解析失败的行
            }
        }

        return items;
    }

    /// <summary>
    /// 异步追加 JSON Line
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="data">数据</param>
    /// <param name="typeInfo">JSON 类型信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task AppendJsonLineAsync<T>(
        string filePath,
        T data,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default)
    {
        EnsureDirectory(filePath);

        // 使用 ArrayPool 减少内存分配
        var bufferWriter = new ArrayBufferWriter<byte>(DefaultBufferSize);
        using var writer = new Utf8JsonWriter(bufferWriter);

        JsonSerializer.Serialize(writer, data, typeInfo);
        await writer.FlushAsync(cancellationToken);

        // 添加换行符
        bufferWriter.Write(System.Text.Encoding.UTF8.GetBytes(Environment.NewLine));

        await AppendBytesWithRetryAsync(filePath, bufferWriter.WrittenMemory, cancellationToken);
    }

    /// <summary>
    /// 异步保存所有数据到 JSON Lines 文件
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="items">数据列表</param>
    /// <param name="typeInfo">JSON 类型信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task SaveJsonLinesAsync<T>(
        string filePath,
        IReadOnlyList<T> items,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default)
    {
        EnsureDirectory(filePath);

        // 使用 ArrayPool 减少内存分配
        var bufferWriter = new ArrayBufferWriter<byte>(DefaultBufferSize * items.Count);
        using var writer = new Utf8JsonWriter(bufferWriter);

        // 手动构建 JSON Lines 格式
        for (int i = 0; i < items.Count; i++)
        {
            JsonSerializer.Serialize(writer, items[i], typeInfo);
            await writer.FlushAsync(cancellationToken);

            // 添加换行符（除了最后一行）
            if (i < items.Count - 1)
            {
                bufferWriter.Write(System.Text.Encoding.UTF8.GetBytes(Environment.NewLine));
            }
        }

        await WriteBytesWithRetryAsync(filePath, bufferWriter.WrittenMemory, cancellationToken);
    }

    /// <summary>
    /// 带重试的字节数组写入
    /// </summary>
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
                await using var stream = new FileStream(
                    filePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    DefaultBufferSize,
                    FileOptions.Asynchronous | FileOptions.WriteThrough);

                await stream.WriteAsync(content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                return;
            }
            catch (IOException) when (retry < maxRetries - 1)
            {
                await Task.Delay(baseDelayMs * (retry + 1), cancellationToken);
            }
        }
    }

    /// <summary>
    /// 带重试的字节数组追加
    /// </summary>
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
                await using var stream = new FileStream(
                    filePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    DefaultBufferSize,
                    FileOptions.Asynchronous);

                await stream.WriteAsync(content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                return;
            }
            catch (IOException) when (retry < maxRetries - 1)
            {
                await Task.Delay(baseDelayMs * (retry + 1), cancellationToken);
            }
        }
    }

    /// <summary>
    /// 安全地删除文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否成功删除</returns>
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
            // 忽略删除失败
        }

        return false;
    }

    /// <summary>
    /// 获取文件大小（字节）
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>文件大小，如果文件不存在则返回 0</returns>
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

    /// <summary>
    /// 生成带时间戳的文件名
    /// </summary>
    /// <param name="baseName">基础名称</param>
    /// <param name="extension">扩展名</param>
    /// <returns>带时间戳的文件名</returns>
    public static string GenerateTimestampedFileName(string baseName, string extension)
    {
        var timestamp = DateTime.Now.ToString(DateTimeFormats.FileTimestamp);
        return $"{baseName}_{timestamp}{extension}";
    }

    /// <summary>
    /// 异步安全地移动文件
    /// </summary>
    /// <param name="sourcePath">源文件路径</param>
    /// <param name="destPath">目标文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功移动</returns>
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
                await Task.Delay(DefaultBaseDelayMs * (retry + 1), cancellationToken);
            }
        }

        return false;
    }

    /// <summary>
    /// 异步复制文件
    /// </summary>
    /// <param name="sourcePath">源文件路径</param>
    /// <param name="destPath">目标文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功复制</returns>
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

                await sourceStream.CopyToAsync(destStream, cancellationToken);
                await destStream.FlushAsync(cancellationToken);
                return true;
            }
            catch (IOException) when (retry < DefaultMaxRetries - 1)
            {
                await Task.Delay(DefaultBaseDelayMs * (retry + 1), cancellationToken);
            }
        }

        return false;
    }

    /// <summary>
    /// 检查文件是否被锁定
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>如果文件被锁定返回 true</returns>
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

    /// <summary>
    /// 等待文件解锁
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="timeout">超时时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果文件解锁返回 true</returns>
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

            await Task.Delay(DefaultBaseDelayMs, cancellationToken);
        }

        return false;
    }

    /// <summary>
    /// 异步写入文本文件（带重试逻辑）
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="content">文本内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task WriteTextAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        EnsureDirectory(filePath);

        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        await WriteBytesWithRetryAsync(filePath, bytes, cancellationToken);
    }

    /// <summary>
    /// 异步读取文本文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件内容，如果文件不存在则返回 null</returns>
    public static async Task<string?> ReadTextAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }
}
