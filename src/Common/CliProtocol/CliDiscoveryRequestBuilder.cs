namespace Common.CliProtocol;

/// <summary>
/// CLI工具发现请求构建器
/// 统一构建 list_tools 命令请求
/// </summary>
public static class CliDiscoveryRequestBuilder
{
    public static string BuildListToolsArguments()
    {
        var request = new Dictionary<string, string>
        {
            ["command"] = "list_tools"
        };
        var jsonParams = JsonSerializer.Serialize(request, CommonJsonContext.Default.DictionaryStringString);
        var base64Input = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonParams));
        return $"--json-input {base64Input}";
    }

    public static string BuildListCommandsArguments()
    {
        var request = new Dictionary<string, string>
        {
            ["command"] = "list_commands"
        };
        var jsonParams = JsonSerializer.Serialize(request, CommonJsonContext.Default.DictionaryStringString);
        var base64Input = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonParams));
        return $"--json-input {base64Input}";
    }

    public static string BuildCommandArguments(IReadOnlyDictionary<string, JsonElement> parameters)
    {
        var jsonParams = JsonSerializer.Serialize(parameters, CommonJsonContext.Default.DictionaryStringJsonElement);
        var base64Input = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonParams));
        return $"--json-input {base64Input}";
    }
}
