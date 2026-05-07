using System.CommandLine;
using AstCli.Commands;
using AstCli.Models;

using var logger = new Logger(
    LogOutput.StdErr,
    LogLevel.Info,
    nameof(AstCli));

var rootCommand = new RootCommand("AST CLI Tool - Code analysis, symbol query, and refactoring");

var jsonInputOption = new Option<string>(
    name: Common.Constants.ConstantManager.Commands.Cli.JsonInput,
    description: "Base64-encoded JSON request")
{
    IsRequired = true
};

var commandOption = new Option<string>(
    name: Common.Constants.ConstantManager.Commands.Cli.Command,
    description: "Command to execute: query_symbol, find_references, rename_symbol, replace_symbol, get_symbol_info, list_tools")
{
    IsRequired = false
};

rootCommand.AddOption(jsonInputOption);
rootCommand.AddOption(commandOption);

rootCommand.SetHandler(async (string jsonInput) =>
{
    if (string.IsNullOrEmpty(jsonInput))
    {
        logger.Error($"Missing required option: {Common.Constants.ConstantManager.Commands.Cli.JsonInput}");
        Environment.Exit(1);
        return;
    }

    try
    {
        var jsonBytes = Convert.FromBase64String(jsonInput);
        var json = Encoding.UTF8.GetString(jsonBytes);

        var request = JsonSerializer.Deserialize(json, AstCliJsonContext.Default.AstCliRequest);

        if (request == null || string.IsNullOrEmpty(request.Command))
        {
            logger.Error("Invalid request: missing command");
            Environment.Exit(1);
            return;
        }

        logger.Debug($"Executing command: {request.Command}");

        var result = await CommandHandler.ExecuteAsync(request);

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
