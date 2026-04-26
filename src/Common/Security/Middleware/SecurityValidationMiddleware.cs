using Common.Middleware;
using Common.Plugins;

using SecurityConstants = Common.Constants.ConstantManager.Security;

namespace Common.Security.Middleware;

/// <summary>
/// 安全验证中间件，负责输入验证和权限检查
/// </summary>
public sealed class SecurityValidationMiddleware : MiddlewareBase
{
    private readonly IInputValidator _inputValidator;
    private readonly IPermissionChecker _permissionChecker;
    private readonly ISecurityLogger _securityLogger;

    /// <summary>
    /// 初始化安全验证中间件
    /// </summary>
    /// <param name="inputValidator">输入验证器</param>
    /// <param name="permissionChecker">权限检查器</param>
    /// <param name="securityLogger">安全日志记录器</param>
    public SecurityValidationMiddleware(
        IInputValidator inputValidator,
        IPermissionChecker permissionChecker,
        ISecurityLogger securityLogger)
    {
        _inputValidator = ValidateService(inputValidator, nameof(inputValidator));
        _permissionChecker = ValidateService(permissionChecker, nameof(permissionChecker));
        _securityLogger = ValidateService(securityLogger, nameof(securityLogger));
    }

    /// <inheritdoc />
    public override async Task InvokeAsync(ToolContext context, Func<Task> next)
    {
        ValidateContext(context, next);

        // 获取用户信息
        var userId = context.GetUserId();
        var userRoles = context.GetUserRoles();

        // 1. 检查工具白名单
        if (!_permissionChecker.IsToolInWhitelist(context.ToolName))
        {
            await _securityLogger.LogToolExecutionBlockedAsync(
                context.ToolName,
                "工具不在白名单中",
                userId);

            ErrorResponseFactory.SetToolBlockedResult(context, context.ToolName, "工具不在白名单中，禁止执行");
            return;
        }

        // 2. 输入验证
        var validationRequest = new InputValidationRequest
        {
            ToolName = context.ToolName,
            Parameters = context.Parameters,
            InputSchema = context.GetInputSchema()
        };

        var validationResult = await _inputValidator.ValidateAsync(validationRequest);

        if (!validationResult.IsValid)
        {
            await _securityLogger.LogInputValidationFailedAsync(
                context.ToolName,
                validationResult.Errors,
                validationResult.DetectedAttacks,
                userId);

            // 如果检测到恶意内容，记录严重安全事件
            if (validationResult.DetectedAttacks.Count > 0)
            {
                foreach (var attackType in validationResult.DetectedAttacks)
                {
                    await _securityLogger.LogMaliciousContentDetectedAsync(
                        context.ToolName,
                        attackType,
                        string.Join("; ", validationResult.Errors),
                        userId);
                }
            }

            ErrorResponseFactory.SetValidationFailedResult(context, validationResult.Errors);
            return;
        }

        // 3. 权限检查
        var permissionRequest = new PermissionCheckRequest
        {
            ToolName = context.ToolName,
            UserId = userId,
            Roles = userRoles,
            RequiredPermissions = context.GetRequiredPermissions(),
            Context = context.GetExecutionContext()
        };

        var permissionResult = await _permissionChecker.CheckPermissionAsync(permissionRequest);

        if (!permissionResult.IsAllowed)
        {
            await _securityLogger.LogPermissionDeniedAsync(
                context.ToolName,
                userId,
                permissionResult.DenyReason ?? "未知原因",
                permissionResult.MissingPermissions);

            ErrorResponseFactory.SetPermissionDeniedResult(
                context,
                permissionResult.DenyReason ?? "未知原因",
                permissionResult.MissingPermissions);

            return;
        }

        // 4. 执行后续中间件
        await next();
    }
}
