namespace Common.IoC;

public sealed class ServiceContainer : IServiceRegistry, IServiceProvider, IDisposable
{
    private readonly struct ServiceRegistration
    {
        public Type ServiceType { get; }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public Type? ImplementationType { get; }

        public object? Instance { get; }
        public ServiceLifetime Lifetime { get; }

        public ServiceRegistration(
            Type serviceType,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type? implementationType,
            object? instance,
            ServiceLifetime lifetime)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
            Instance = instance;
            Lifetime = lifetime;
        }
    }

    private readonly ConcurrentDictionary<Type, ServiceRegistration> _registrations = new();
    private readonly ConcurrentDictionary<Type, object> _singletons = new();
    private readonly ConcurrentDictionary<Type, Lock> _singletonLocks = new();

    public void AddSingleton<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        var serviceType = typeof(TService);
        var registration = new ServiceRegistration(
            serviceType,
            typeof(TImplementation),
            null,
            ServiceLifetime.Singleton);

        _registrations[serviceType] = registration;
    }

    public void AddTransient<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        var serviceType = typeof(TService);
        var registration = new ServiceRegistration(
            serviceType,
            typeof(TImplementation),
            null,
            ServiceLifetime.Transient);

        _registrations[serviceType] = registration;
    }

    public void AddInstance<TService>(TService instance)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);

        var serviceType = typeof(TService);
        var registration = new ServiceRegistration(
            serviceType,
            null,
            instance,
            ServiceLifetime.Singleton);

        _registrations[serviceType] = registration;
        _singletons[serviceType] = instance;
    }

    /// <summary>
    /// 使用工厂委托注册单例服务
    /// </summary>
    public void AddSingleton<TService>(Func<IServiceProvider, TService> factory)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        var serviceType = typeof(TService);
        var registration = new ServiceRegistration(
            serviceType,
            null,
            new FactoryHolder<TService> { Factory = factory },
            ServiceLifetime.Singleton);

        _registrations[serviceType] = registration;
    }

    /// <summary>
    /// 注册单例服务（仅服务类型）
    /// </summary>
    public void AddSingleton<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>()
        where TService : class
    {
        var serviceType = typeof(TService);
        var registration = new ServiceRegistration(
            serviceType,
            typeof(TService),
            null,
            ServiceLifetime.Singleton);

        _registrations[serviceType] = registration;
    }

    /// <summary>
    /// 工厂持有者接口 - 用于AOT兼容的工厂调用
    /// </summary>
    private interface IFactoryHolder
    {
        object Invoke(IServiceProvider serviceProvider);
    }

    /// <summary>
    /// 工厂委托持有者，用于存储工厂方法
    /// </summary>
    private sealed class FactoryHolder<T> : IFactoryHolder where T : class
    {
        public Func<IServiceProvider, T>? Factory { get; init; }

        public object Invoke(IServiceProvider serviceProvider)
        {
            if (Factory is null)
            {
                throw new InvalidOperationException($"Factory for type '{typeof(T).Name}' is not set.");
            }
            return Factory(serviceProvider);
        }
    }

    public T GetService<T>()
        where T : class
    {
        return (T)GetService(typeof(T));
    }

    public object GetService(Type serviceType)
    {
        return ResolveService(serviceType, null);
    }

    private object ResolveService(Type serviceType, HashSet<Type>? resolutionChain)
    {
        resolutionChain ??= [];

        if (!resolutionChain.Add(serviceType))
        {
            throw new InvalidOperationException(
                $"Circular dependency detected for service '{serviceType.FullName}'. " +
                $"Resolution chain: {string.Join(" -> ", resolutionChain.Select(t => t.Name))}");
        }

        if (!_registrations.TryGetValue(serviceType, out var registration))
        {
            throw new InvalidOperationException(
                $"Service '{serviceType.FullName}' is not registered.");
        }

        if (registration.Lifetime == ServiceLifetime.Singleton)
        {
            return ResolveSingleton(registration, resolutionChain);
        }

        return CreateInstance(registration.ImplementationType!, resolutionChain);
    }

    private object ResolveSingleton(ServiceRegistration registration, HashSet<Type> resolutionChain)
    {
        var serviceType = registration.ServiceType;

        if (_singletons.TryGetValue(serviceType, out var existingInstance))
        {
            return existingInstance;
        }

        var lockObj = _singletonLocks.GetOrAdd(serviceType, _ => new Lock());

        lock (lockObj)
        {
            if (_singletons.TryGetValue(serviceType, out existingInstance))
            {
                return existingInstance;
            }

            object instance;
            if (registration.Instance is not null)
            {
                // 检查是否是工厂委托
                var instanceType = registration.Instance.GetType();
                if (instanceType.IsGenericType && instanceType.GetGenericTypeDefinition() == typeof(FactoryHolder<>))
                {
                    instance = InvokeFactoryHolder(registration.Instance, this);
                }
                else
                {
                    instance = registration.Instance;
                }
            }
            else
            {
                instance = CreateInstance(registration.ImplementationType!, resolutionChain);
            }

            _singletons[serviceType] = instance;
            return instance;
        }
    }

    private object CreateInstance(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType,
        HashSet<Type> resolutionChain)
    {
        var constructors = implementationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        if (constructors.Length == 0)
        {
            throw new InvalidOperationException(
                $"Type '{implementationType.FullName}' does not have a public constructor.");
        }

        var constructor = constructors
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        var parameters = constructor.GetParameters();
        var arguments = new object[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            arguments[i] = ResolveService(paramType, new HashSet<Type>(resolutionChain));
        }

        return constructor.Invoke(arguments);
    }

    /// <summary>
    /// 调用工厂持有者 - 使用接口避免反射
    /// </summary>
    private static object InvokeFactoryHolder(object factoryHolder, IServiceProvider serviceProvider)
    {
        if (factoryHolder is IFactoryHolder holder)
        {
            return holder.Invoke(serviceProvider);
        }
        throw new InvalidOperationException("Factory holder does not implement IFactoryHolder interface.");
    }

    public void Dispose()
    {
        foreach (var singleton in _singletons.Values)
        {
            if (singleton is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _singletons.Clear();
        _registrations.Clear();
        _singletonLocks.Clear();
    }
}
