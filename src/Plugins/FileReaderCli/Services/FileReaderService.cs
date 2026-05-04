using Common.Contracts.Models;
using AsyncFileLock;

namespace FileReaderCli.Services;

internal sealed class FileReaderService
{
    public async Task<FileReadResult> ReadFileLinesAsync(string filePath, int lineCount)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty", nameof(filePath));
        }

        if (!Path.IsPathRooted(filePath))
        {
            filePath = Path.Combine(AppContext.BaseDirectory, filePath);
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var lines = new List<string>();
        var totalLines = 0;

        var lockResult = await FileLockService.AcquireAsync(filePath, TimeSpan.FromSeconds(5));
        if (!lockResult.Success || lockResult.Lock == null)
        {
            throw new TimeoutException($"Failed to acquire lock for file: {filePath}");
        }

        await using (var batchLock = lockResult.Lock)
        {
#pragma warning disable MCP001
            using var reader = new StreamReader(filePath, Encoding.UTF8);
            string? line;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                totalLines++;
                if (lines.Count < lineCount)
                {
                    lines.Add(line);
                }
            }
#pragma warning restore MCP001
        }

        return new FileReadResult
        {
            FilePath = filePath,
            Lines = lines,
            TotalLines = totalLines,
            RequestedLines = lineCount
        };
    }

    public async Task<FileReadResult> ReadFileHeadAsync(string filePath, int lineCount)
    {
        return await ReadFileLinesAsync(filePath, lineCount);
    }

    public async Task<FileReadResult> ReadFileTailAsync(string filePath, int lineCount)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty", nameof(filePath));
        }

        if (!Path.IsPathRooted(filePath))
        {
            filePath = Path.Combine(AppContext.BaseDirectory, filePath);
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        string[] allLines;

        var lockResult = await FileLockService.AcquireAsync(filePath, TimeSpan.FromSeconds(5));
        if (!lockResult.Success || lockResult.Lock == null)
        {
            throw new TimeoutException($"Failed to acquire lock for file: {filePath}");
        }

        await using (var batchLock = lockResult.Lock)
        {
#pragma warning disable MCP001
            allLines = await File.ReadAllLinesAsync(filePath);
#pragma warning restore MCP001
        }

        var totalLines = allLines.Length;
        var lines = allLines
            .Skip(Math.Max(0, totalLines - lineCount))
            .Take(lineCount)
            .ToList();

        return new FileReadResult
        {
            FilePath = filePath,
            Lines = lines,
            TotalLines = totalLines,
            RequestedLines = lineCount
        };
    }
}
