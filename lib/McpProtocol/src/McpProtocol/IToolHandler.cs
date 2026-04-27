namespace McpProtocol;

public interface IToolHandler
{
    string Name { get; }
    string Description { get; }
    Task<object> ExecuteAsync(Dictionary<string, JsonElement> arguments);
}
