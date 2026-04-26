using SecurityConstants = Common.Constants.ConstantManager.Security;

namespace Common.Security.Extensions;

/// <summary>
/// 服务容器扩展方法，用于注册安全服务
/// </summary>
public static class SecurityServiceExtensions
{
    /// <summary>
    /// 添加安全验证服务
    /// </summary>
    /// <param name="services">服务注册器</param>
    /// <param name="whitelistConfig">白名单配置（可选）</param>
    /// <param name="rbacConfig">RBAC配置（可选）</param>
    /// <returns>服务注册器</returns>
    public static IServiceRegistry AddSecurityServices(
        this IServiceRegistry services,
        WhitelistConfiguration? whitelistConfig = null,
        RbacConfiguration? rbacConfig = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // 注册配置
        whitelistConfig ??= SecurityConfigurationLoader.CreateDefaultWhitelistConfiguration();
        rbacConfig ??= SecurityConfigurationLoader.CreateDefaultRbacConfiguration();

        services.AddInstance(whitelistConfig);
        services.AddInstance(rbacConfig);

        // 注册核心服务
        services.AddSingleton<IInputValidator, JsonSchemaValidator>();
        services.AddSingleton<IPermissionChecker, RbacPermissionChecker>();
        services.AddSingleton<ISecurityLogger, SecurityAuditLogger>();
        services.AddSingleton<ISecurityService, SecurityService>();

        // 注册中间件
        services.AddSingleton<SecurityValidationMiddleware, SecurityValidationMiddleware>();

        return services;
    }

    /// <summary>
    /// 从配置文件添加安全验证服务
    /// </summary>
    /// <param name="services">服务注册器</param>
    /// <param name="whitelistConfigPath">白名单配置文件路径</param>
    /// <param name="rbacConfigPath">RBAC配置文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>服务注册器</returns>
    public static async Task<IServiceRegistry> AddSecurityServicesFromConfigAsync(
        this IServiceRegistry services,
        string whitelistConfigPath,
        string rbacConfigPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var whitelistConfig = await SecurityConfigurationLoader.LoadWhitelistConfigurationAsync(whitelistConfigPath, cancellationToken);
        var rbacConfig = await SecurityConfigurationLoader.LoadRbacConfigurationAsync(rbacConfigPath, cancellationToken);

        return services.AddSecurityServices(whitelistConfig, rbacConfig);
    }

    /// <summary>
    /// 添加安全验证中间件到管道
    /// </summary>
    /// <param name="pipeline">中间件管道</param>
    /// <param name="serviceProvider">服务提供器</param>
    /// <returns>中间件管道</returns>
    public static IMiddlewarePipeline UseSecurityValidation(
        this IMiddlewarePipeline pipeline,
        Common.IoC.IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var middleware = serviceProvider.GetService<SecurityValidationMiddleware>();
        if (middleware is null)
        {
            throw new InvalidOperationException("SecurityValidationMiddleware未注册，请先调用AddSecurityServices");
        }

        pipeline.Use(middleware.InvokeAsync);
        return pipeline;
    }
}
