using SecurityConstants = Common.Constants.ConstantManager.Security;

namespace Common.Security.Models;

/// <summary>
/// 输入验证请求
/// </summary>
public sealed class InputValidationRequest
{
    /// <summary>
    /// 工具名称
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// 输入参数
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> Parameters { get; init; } = new Dictionary<string, JsonElement>();

    /// <summary>
    /// 输入Schema
    /// </summary>
    public JsonElement InputSchema { get; init; }
}

/// <summary>
/// 权限检查请求
/// </summary>
public sealed class PermissionCheckRequest
{
    /// <summary>
    /// 工具名称
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// 用户ID
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// 用户角色列表
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// 所需权限列表
    /// </summary>
    public IReadOnlyList<string> RequiredPermissions { get; init; } = [];

    /// <summary>
    /// 执行上下文
    /// </summary>
    public Dictionary<string, string> Context { get; init; } = [];
}

// PermissionCheckResult 已合并到 PermissionResult.cs
