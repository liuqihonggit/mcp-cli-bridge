using Common.FileLock;

namespace Common.Cli;

public abstract class CliServiceBase : IDisposable
{
    protected ServiceContainer Container { get; }
    protected ILogger Logger { get; }

    protected CliServiceBase(string serviceName)
    {
        Container = new ServiceContainer();

        Container.AddFileAccessService();
        Container.AddInstance<ILogger>(new Logger(LogOutput.StdErr, LogLevel.Info, serviceName));

        Logger = Container.GetService<ILogger>();
    }

    public void Dispose()
    {
        Container.Dispose();
        GC.SuppressFinalize(this);
    }
}

public abstract class CliServiceBase<TOptions> : IDisposable where TOptions : class, new()
{
    protected ServiceContainer Container { get; }
    protected ILogger Logger { get; }
    protected TOptions ServiceOptions { get; }

    protected CliServiceBase(string serviceName, TOptions? serviceOptions = null)
    {
        ServiceOptions = serviceOptions ?? new TOptions();

        Container = new ServiceContainer();
        Container.AddFileAccessService();
        Container.AddInstance(new Logger(LogOutput.StdErr, LogLevel.Info, serviceName));
        Container.AddInstance(ServiceOptions);

        Logger = Container.GetService<ILogger>();
    }

    public void Dispose()
    {
        Container.Dispose();
        GC.SuppressFinalize(this);
    }
}
