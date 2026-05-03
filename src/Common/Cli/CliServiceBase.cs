using FileLock.Contracts;
using FileLock;
using Common.FileLock;

namespace Common.Cli;

public abstract class CliServiceBase : IDisposable
{
    protected ServiceContainer Container { get; }
    protected IFileAccessService FileAccess { get; }
    protected ILogger Logger { get; }
    protected FileAccessOptions Options { get; }

    protected CliServiceBase(string serviceName, FileAccessOptions? options = null)
    {
        Options = options ?? new FileAccessOptions();
        Container = new ServiceContainer();

        Container.AddFileAccessServices(Options);
        Container.AddInstance<ILogger>(new Logger(LogOutput.StdErr, LogLevel.Info, serviceName));

        FileAccess = Container.GetService<IFileAccessService>();
        Logger = Container.GetService<ILogger>();
    }

    protected CliServiceBase(string serviceName, Action<FileAccessOptions> configure)
    {
        Options = new FileAccessOptions();
        configure(Options);

        Container = new ServiceContainer();
        Container.AddFileAccessServices(Options);
        Container.AddInstance<ILogger>(new Logger(LogOutput.StdErr, LogLevel.Info, serviceName));

        FileAccess = Container.GetService<IFileAccessService>();
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
    protected IFileAccessService FileAccess { get; }
    protected ILogger Logger { get; }
    protected TOptions ServiceOptions { get; }
    protected FileAccessOptions FileAccessOptions { get; }

    protected CliServiceBase(string serviceName, TOptions? serviceOptions = null, FileAccessOptions? fileAccessOptions = null)
    {
        ServiceOptions = serviceOptions ?? new TOptions();
        FileAccessOptions = fileAccessOptions ?? new FileAccessOptions();

        Container = new ServiceContainer();
        Container.AddFileAccessServices(FileAccessOptions);
        Container.AddInstance(new Logger(LogOutput.StdErr, LogLevel.Info, serviceName));
        Container.AddInstance(ServiceOptions);

        FileAccess = Container.GetService<IFileAccessService>();
        Logger = Container.GetService<ILogger>();
    }

    public void Dispose()
    {
        Container.Dispose();
    }
}
