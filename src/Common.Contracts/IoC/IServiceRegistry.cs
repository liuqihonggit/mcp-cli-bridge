namespace Common.Contracts.IoC;

public interface IServiceRegistry
{
    void AddSingleton<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
        where TService : class
        where TImplementation : class, TService;

    void AddTransient<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
        where TService : class
        where TImplementation : class, TService;

    void AddInstance<TService>(TService instance)
        where TService : class;
}
