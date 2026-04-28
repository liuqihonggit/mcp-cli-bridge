using System.Text.Json;
using Common.Contracts.Models;
using Common.Json;
using Common.Results;

using var logger = new Logger(
    LogOutput.StdErr,
    LogLevel.Info,
    nameof(MemoryCli));

var rulesInstaller = new RulesInstallerService();
await rulesInstaller.InstallDefaultRulesAsync();

var rootCommand = new RootCommand("Knowledge Graph CLI Tool - Manage entities and relations");

var jsonInputOption = new Option<string>(
    name: Commands.Cli.JsonInput,
    description: "Base64-encoded JSON request")
{
    IsRequired = true
};

var commandOption = new Option<string>(
    name: Commands.Cli.Command,
    description: "Command to execute: " + string.Join(", ", new[]
    {
        Commands.Memory.CreateEntities,
        Commands.Memory.CreateRelations,
        Commands.Memory.ReadGraph,
        Commands.Memory.SearchNodes,
        Commands.Memory.AddObservations,
        Commands.Memory.DeleteEntities,
        Commands.Memory.OpenNodes,
        Commands.Memory.GetStorageInfo
    }))
{
    IsRequired = false
};

rootCommand.AddOption(jsonInputOption);
rootCommand.AddOption(commandOption);

rootCommand.SetHandler(async (string jsonInput) =>
{
    if (string.IsNullOrEmpty(jsonInput))
    {
        logger.Error($"Missing required option: {Commands.Cli.JsonInput}");
        Environment.Exit(1);
        return;
    }

    try
    {
        var jsonBytes = Convert.FromBase64String(jsonInput);
        var json = System.Text.Encoding.UTF8.GetString(jsonBytes);

        var request = JsonSerializer.Deserialize(json, CommonJsonContext.Default.CliRequest);

        if (request == null || string.IsNullOrEmpty(request.Command))
        {
            logger.Error("Invalid request: missing command");
            Environment.Exit(1);
            return;
        }

        logger.Debug($"Executing command: {request.Command}");

        var options = new MemoryOptions();
        using var ioService = new MemoryIoService(options);
        var handler = new CommandHandler(ioService, options);
        var result = await handler.ExecuteAsync(request);

        // 使用 Source Generator 序列化 OperationResult<JsonElement>
        Console.WriteLine(JsonSerializer.Serialize(result, CommonJsonContext.Default.OperationResultJsonElement));
        Environment.Exit(result.Success ? 0 : 1);
    }
    catch (Exception ex)
    {
        logger.Error(ex, "Command execution failed");

        var errorResult = new OperationResult<JsonElement>
        {
            Success = false,
            Message = ex.Message,
            Data = default
        };
        Console.WriteLine(JsonSerializer.Serialize(errorResult, CommonJsonContext.Default.OperationResultJsonElement));
        Environment.Exit(1);
    }
}, jsonInputOption);

return rootCommand.Invoke(args);
