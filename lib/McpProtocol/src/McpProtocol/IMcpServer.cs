namespace McpProtocol;

public interface IMcpServer
{
    void RegisterTool<T>(T toolInstance) where T : class;
    Task RunAsync(CancellationToken cancellationToken = default);
}
