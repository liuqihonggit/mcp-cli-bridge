using SecurityConstants = Common.Constants.ConstantManager.Security;

namespace Common.Security.Examples;

public static class SecurityUsageExample
{
    public static void Example1_RegisterSecurityServices()
    {
        var services = new ServiceContainer();

        services.AddSecurityServices();

        var whitelistConfig = new WhitelistConfiguration
        {
            AllowedTools = { "memory_create_entities", "memory_read_graph" },
            AllowedCliCommands = { "MemoryCli" },
            IsEnabled = true
        };

        var rbacConfig = new RbacConfiguration
        {
            RolePermissions = new Dictionary<string, HashSet<string>>
            {
                ["admin"] = ["read", "write", "delete", "execute", "admin"]
            },
            UserRoles = new Dictionary<string, HashSet<string>>
            {
                ["user@example.com"] = ["admin"]
            },
            IsEnabled = true
        };

        services.AddSecurityServices(whitelistConfig, rbacConfig);
    }

    public static void Example2_ConfigureMiddlewarePipeline(Common.IoC.IServiceProvider serviceProvider)
    {
        var pipeline = new MiddlewarePipeline(serviceProvider);

        pipeline.UseSecurityValidation(serviceProvider);
    }

    public static void Example3_SetUserContext(ToolContext context)
    {
        context.Items["UserId"] = "user@example.com";
        context.Items["UserRoles"] = new List<string> { "admin", "power_user" };
        context.Items["RequestId"] = Guid.NewGuid().ToString();
        context.Items["Source"] = "MCP-Client";
    }

    public static async Task Example4_ManualValidation(ISecurityService securityService)
    {
        var validationResult = await securityService.ValidateInputAsync(
            "memory_create_entities",
            new Dictionary<string, JsonElement>(),
            JsonConstants.EmptyObject);

        if (!validationResult.IsValid)
        {
            Console.WriteLine($"验证失败: {string.Join(", ", validationResult.Errors)}");
        }

        var permissionResult = await securityService.CheckPermissionAsync(
            "memory_delete_entities",
            "user@example.com",
            new List<string> { "delete" });

        if (!permissionResult.IsAllowed)
        {
            Console.WriteLine($"权限拒绝: {permissionResult.DenyReason}");
        }

        await securityService.LogSecurityEventAsync(new SecurityAuditEntry
        {
            EventType = "CUSTOM_EVENT",
            ToolName = "memory_create_entities",
            UserId = "user@example.com",
            IsSuccess = true,
            Message = "自定义安全事件"
        });

        var logs = await securityService.GetAuditLogsAsync(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow);

        foreach (var log in logs)
        {
            Console.WriteLine($"[{log.Timestamp}] {log.EventType}: {log.Message}");
        }
    }
}
