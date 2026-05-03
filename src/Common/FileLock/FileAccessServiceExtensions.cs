using Common.Contracts.IoC;
using FileLock;
using FileLock.Contracts;

namespace Common.FileLock;

public static class FileAccessServiceExtensions
{
    public static void AddFileAccessServices(
        this IServiceRegistry registry,
        FileAccessOptions? options = null)
    {
        options ??= new FileAccessOptions();

        registry.AddInstance(options);
        registry.AddSingleton<IFileLockProvider, HybridFileLockProvider>();
        registry.AddSingleton<IFileAccessService, FileAccessService>();
    }

    public static void AddFileAccessServices(
        this IServiceRegistry registry,
        Action<FileAccessOptions> configure)
    {
        var options = new FileAccessOptions();
        configure(options);

        registry.AddFileAccessServices(options);
    }

    public static void AddCustomFileLockProvider<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TLockProvider>(
        this IServiceRegistry registry,
        FileAccessOptions? options = null)
        where TLockProvider : class, IFileLockProvider
    {
        options ??= new FileAccessOptions();

        registry.AddInstance(options);
        registry.AddSingleton<IFileLockProvider, TLockProvider>();
        registry.AddSingleton<IFileAccessService, FileAccessService>();
    }
}
