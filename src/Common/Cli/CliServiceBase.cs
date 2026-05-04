using Common.FileLock;

namespace Common.Cli;

public abstract class CliServiceBase : IDisposable
{
    protected ServiceContainer Container { get; }
    protected Common.FileLock.FileLockService FileLockService { get; }
    protected ILogger Logger { get; }

    protected CliServiceBase(string serviceName)
    {
        Container = new ServiceContainer();

        Container.AddFileAccessService();
        Container.AddInstance<ILogger>(new Logger(LogOutput.StdErr, LogLevel.Info, serviceName));

        FileLockService = Container.GetService<Common.FileLock.FileLockService>();
        Logger = Container.GetService<ILogger>();
    }

    public void Dispose()
    {
        Container.Dispose();
    }
}

public abstract class CliServiceBase<TOptions> : IDisposable where TOptions : class, new()
{
    protected ServiceContainer Container { get; }
    protected Common.FileLock.FileLockService FileLockService { get; }
    protected ILogger Logger { get; }
    protected TOptions ServiceOptions { get; }

    protected CliServiceBase(string serviceName, TOptions? serviceOptions = null)
    {
        ServiceOptions = serviceOptions ?? new TOptions();

        Container = new ServiceContainer();
        Container.AddFileAccessService();
        Container.AddInstance(new Logger(LogOutput.StdErr, LogLevel.Info, serviceName));
        Container.AddInstance(ServiceOptions);

        FileLockService = Container.GetService<Common.FileLock.FileLockService>();
        Logger = Container.GetService<ILogger>();
    }

    public void Dispose()
    {
        Container.Dispose();
    }
}
