namespace Common.IoC;

public interface IServiceProvider
{
    T GetService<T>()
        where T : class;

    object GetService(Type type);
}
