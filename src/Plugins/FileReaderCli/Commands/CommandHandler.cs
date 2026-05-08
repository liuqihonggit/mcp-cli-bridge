using Common.Contracts.Attributes;
using FileReaderCli.Schemas;

namespace FileReaderCli.Commands;

[CliCommandHandler("file_reader_cli", "File Reader CLI - Read file contents (head/tail) with line control", Category = "file-operations", ToolNamePrefix = "file_reader_", HasDocumentation = true)]
internal sealed partial class CommandHandler
{
    [CliCommand("read_head", Description = "Read the first N lines from a file", Category = "file-operations", SchemaType = typeof(FileReaderSchemas.ReadHead))]
    private static async Task<OperationResult<JsonElement>> ReadHeadAsync(FileReaderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
            return Fail("File path is required");

        try
        {
            var result = await FileReaderService.ReadFileHeadAsync(request.FilePath, request.LineCount);
            return Ok(result, $"Read {result.Lines.Count} lines from {result.FilePath} (total: {result.TotalLines} lines)", CommonJsonContext.Default.FileReadResult);
        }
        catch (FileNotFoundException ex)
        {
            return Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return Fail($"Error reading file: {ex.Message}");
        }
    }

    [CliCommand("read_tail", Description = "Read the last N lines from a file", Category = "file-operations", SchemaType = typeof(FileReaderSchemas.ReadTail))]
    private static async Task<OperationResult<JsonElement>> ReadTailAsync(FileReaderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
            return Fail("File path is required");

        try
        {
            var result = await FileReaderService.ReadFileTailAsync(request.FilePath, request.LineCount);
            return Ok(result, $"Read last {result.Lines.Count} lines from {result.FilePath} (total: {result.TotalLines} lines)", CommonJsonContext.Default.FileReadResult);
        }
        catch (FileNotFoundException ex)
        {
            return Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return Fail($"Error reading file: {ex.Message}");
        }
    }

    private static OperationResult<JsonElement> Fail(string message)
    {
        return new OperationResult<JsonElement>
        {
            Success = false,
            Message = message,
            Data = McpJsonSerializer.EmptyObject
        };
    }

    private static OperationResult<JsonElement> Ok<T>(T data, string message = "", JsonTypeInfo<T> typeInfo = null!)
    {
        return new OperationResult<JsonElement>
        {
            Success = true,
            Message = message,
            Data = JsonSerializer.SerializeToElement(data, typeInfo)
        };
    }
}
