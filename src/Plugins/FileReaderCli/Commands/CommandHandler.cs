using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using FileReaderCli.Services;
using Common.CliProtocol;
using Common.Results;
using Common.Tools;
using Service.Json.Contracts;
using static Common.CliProtocol.FileReaderToolSchemaTemplates;

namespace FileReaderCli.Commands;

internal sealed class CommandHandler
{
    private readonly FileReaderService _fileReaderService;

    public CommandHandler(FileReaderService fileReaderService)
    {
        _fileReaderService = fileReaderService ?? throw new ArgumentNullException(nameof(fileReaderService));
    }

    public async Task<OperationResult<JsonElement>> ExecuteAsync(FileReaderRequest request)
    {
        return request.Command?.ToLowerInvariant() switch
        {
            "read_head" => await ReadHeadAsync(request),
            "read_tail" => await ReadTailAsync(request),
            "list_tools" => ListTools(),
            "list_commands" => ListCommands(),
            _ => Fail($"Unknown command: {request.Command}")
        };
    }

    private static OperationResult<JsonElement> Fail(string message)
    {
        return new OperationResult<JsonElement>
        {
            Success = false,
            Message = message,
            Data = JsonConstants.EmptyObject
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

    private async Task<OperationResult<JsonElement>> ReadHeadAsync(FileReaderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
            return Fail("File path is required");

        try
        {
            var result = await _fileReaderService.ReadFileHeadAsync(request.FilePath, request.LineCount);
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

    private async Task<OperationResult<JsonElement>> ReadTailAsync(FileReaderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
            return Fail("File path is required");

        try
        {
            var result = await _fileReaderService.ReadFileTailAsync(request.FilePath, request.LineCount);
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

    private static OperationResult<JsonElement> ListTools()
    {
        var pluginDescriptor = new PluginDescriptor
        {
            Name = "file_reader",
            Description = "File Reader CLI - Read file contents (head/tail) with line control",
            Category = "file-operations",
            CommandCount = 2,
            HasDocumentation = true
        };

        return Ok(pluginDescriptor, "", CommonJsonContext.Default.PluginDescriptor);
    }

    private static OperationResult<JsonElement> ListCommands()
    {
        var tools = new List<ToolDefinition>
        {
            new()
            {
                Name = "file_reader_read_head",
                Description = "Read the first N lines from a file",
                Category = "file-operations",
                InputSchema = ReadHeadSchema()
            },
            new()
            {
                Name = "file_reader_read_tail",
                Description = "Read the last N lines from a file",
                Category = "file-operations",
                InputSchema = ReadTailSchema()
            }
        };

        return Ok(tools, "", CommonJsonContext.Default.ListToolDefinition);
    }
}
