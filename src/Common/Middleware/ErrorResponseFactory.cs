using Common.Tools;

namespace Common.Middleware;

/// <summary>
/// 错误响应工厂，统一创建各种错误响应的序列化结果
/// </summary>
public static class ErrorResponseFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// 创建工具被阻止的错误响应
    /// </summary>
    public static string ToolBlocked(string toolName, string reason)
    {
        var response = OperationResultFactoryNonGeneric.Fail($"[TOOL_BLOCKED] 工具 '{toolName}' 被阻止: {reason}");
        return JsonSerializer.Serialize(response, CommonJsonContext.Default.OperationResult);
    }

    /// <summary>
    /// 创建验证失败的错误响应
    /// </summary>
    public static string ValidationFailed(IEnumerable<string> errors)
    {
        var errorMessage = $"[VALIDATION_FAILED] {string.Join("; ", errors)}";
        var response = OperationResultFactoryNonGeneric.Fail(errorMessage);
        return JsonSerializer.Serialize(response, CommonJsonContext.Default.OperationResult);
    }

    /// <summary>
    /// 创建验证失败的错误响应（带字段详情）
    /// </summary>
    public static string ValidationFailed(IEnumerable<(string Field, string Message)> errors)
    {
        var errorMessages = errors.Select(e => $"{e.Field}: {e.Message}");
        return ValidationFailed(errorMessages);
    }

    /// <summary>
    /// 创建权限被拒绝的错误响应
    /// </summary>
    public static string PermissionDenied(string? reason = null, IEnumerable<string>? missingPermissions = null)
    {
        var missingPerms = missingPermissions?.ToList() ?? [];

        if (missingPerms.Count > 0)
        {
            var permList = string.Join(", ", missingPerms);
            var message = $"[PERMISSION_DENIED] {reason ?? "权限不足"} (缺失权限: {permList})";
            var response = OperationResultFactoryNonGeneric.Fail(message);
            return JsonSerializer.Serialize(response, CommonJsonContext.Default.OperationResult);
        }

        var simpleResponse = OperationResultFactoryNonGeneric.Fail($"[PERMISSION_DENIED] {reason ?? "权限不足"}");
        return JsonSerializer.Serialize(simpleResponse, CommonJsonContext.Default.OperationResult);
    }

    /// <summary>
    /// 创建工具未找到的错误响应
    /// </summary>
    public static string ToolNotFound(string toolName)
    {
        var result = OperationResultFactoryNonGeneric.CliFailure($"工具 '{toolName}' 未找到", -1);
        return JsonSerializer.Serialize(result, CommonJsonContext.Default.OperationResult);
    }

    /// <summary>
    /// 创建执行错误的错误响应（使用 ExceptionMessageFormatter 统一格式化）
    /// </summary>
    public static string ExecutionError(string toolName, Exception ex)
    {
        var errorMessage = ExceptionMessageFormatter.Format(ex);
        var errorCode = ExceptionMessageFormatter.GetErrorCode(ex);
        var result = OperationResultFactoryNonGeneric.Fail($"[{errorCode}] {errorMessage}");
        return JsonSerializer.Serialize(result, CommonJsonContext.Default.OperationResult);
    }

    /// <summary>
    /// 创建通用错误响应
    /// </summary>
    public static string Error(string errorCode, string message)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteString("error", errorCode);
        writer.WriteString("message", message);
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// 创建带详细错误列表的响应
    /// </summary>
    public static string ErrorWithDetails(string errorCode, string message, IEnumerable<string> errors)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteString("error", errorCode);
        writer.WriteString("message", message);
        writer.WriteStartArray("errors");
        foreach (var error in errors)
        {
            writer.WriteStringValue(error);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// 创建带缺失权限列表的响应
    /// </summary>
    public static string PermissionDeniedWithDetails(string? reason, IEnumerable<string> missingPermissions)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteString("error", "PERMISSION_DENIED");
        writer.WriteString("message", reason ?? "权限不足");
        writer.WriteStartArray("missingPermissions");
        foreach (var permission in missingPermissions)
        {
            writer.WriteStringValue(permission);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// 设置工具被阻止的结果到上下文
    /// </summary>
    public static void SetToolBlockedResult(ToolContext context, string toolName, string reason)
    {
        context.Result = ToolBlocked(toolName, reason);
        context.IsCancelled = true;
    }

    /// <summary>
    /// 设置验证失败的结果到上下文
    /// </summary>
    public static void SetValidationFailedResult(ToolContext context, IEnumerable<string> errors)
    {
        context.Result = ValidationFailed(errors);
        context.IsCancelled = true;
    }

    /// <summary>
    /// 设置验证失败的结果到上下文（带字段详情）
    /// </summary>
    public static void SetValidationFailedResult(ToolContext context, IEnumerable<(string Field, string Message)> errors)
    {
        context.Result = ValidationFailed(errors);
        context.IsCancelled = true;
    }

    /// <summary>
    /// 设置权限被拒绝的结果到上下文
    /// </summary>
    public static void SetPermissionDeniedResult(ToolContext context, string? reason = null, IEnumerable<string>? missingPermissions = null)
    {
        context.Result = PermissionDenied(reason, missingPermissions);
        context.IsCancelled = true;
    }

    /// <summary>
    /// 设置工具未找到的结果到上下文
    /// </summary>
    public static void SetToolNotFoundResult(ToolContext context, string toolName)
    {
        context.Result = ToolNotFound(toolName);
        context.IsCancelled = true;
    }

    /// <summary>
    /// 设置执行错误的结果到上下文
    /// </summary>
    public static void SetExecutionErrorResult(ToolContext context, string toolName, Exception ex)
    {
        context.Result = ExecutionError(toolName, ex);
    }
}
