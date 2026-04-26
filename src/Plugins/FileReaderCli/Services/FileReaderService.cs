using Service.Json.Contracts;

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

        var allLines = await File.ReadAllLinesAsync(filePath);
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
