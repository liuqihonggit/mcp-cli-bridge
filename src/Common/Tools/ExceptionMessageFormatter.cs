namespace Common.Tools;

/// <summary>
/// 异常消息格式化帮助类
/// 统一所有异常消息格式化逻辑
/// </summary>
public static class ExceptionMessageFormatter
{
    /// <summary>
    /// 格式化异常为友好的错误消息
    /// </summary>
    /// <param name="ex">异常</param>
    /// <returns>格式化的错误消息</returns>
    public static string Format(Exception ex)
    {
        return ex switch
        {
            System.ArgumentNullException nullEx => $"参数不能为空: {nullEx.Message}",
            System.ArgumentOutOfRangeException rangeEx => $"参数超出范围: {rangeEx.Message}",
            System.ArgumentException argEx => $"参数错误: {argEx.Message}",
            System.InvalidOperationException invEx => $"操作无效: {invEx.Message}",
            System.Collections.Generic.KeyNotFoundException keyEx => $"未找到资源: {keyEx.Message}",
            System.TimeoutException timeoutEx => $"操作超时: {timeoutEx.Message}",
            System.IO.IOException ioEx => $"IO错误: {ioEx.Message}",
            System.UnauthorizedAccessException authEx => $"权限不足: {authEx.Message}",
            System.Security.SecurityException secEx => $"安全错误: {secEx.Message}",
            System.Text.Json.JsonException jsonEx => $"JSON解析错误: {jsonEx.Message}",
            System.FormatException fmtEx => $"格式错误: {fmtEx.Message}",
            System.NotSupportedException nsEx => $"不支持的操作: {nsEx.Message}",
            System.NotImplementedException niEx => $"功能未实现: {niEx.Message}",
            System.Threading.Tasks.TaskCanceledException => "操作已取消",
            System.OperationCanceledException => "操作已取消",
            System.AggregateException aggEx => FormatAggregate(aggEx),
            _ => $"执行错误: {ex.Message}"
        };
    }

    /// <summary>
    /// 格式化异常为详细的错误消息（包含异常类型）
    /// </summary>
    /// <param name="ex">异常</param>
    /// <returns>详细的错误消息</returns>
    public static string FormatDetailed(Exception ex)
    {
        var baseMessage = Format(ex);
        return $"[{ex.GetType().Name}] {baseMessage}";
    }

    /// <summary>
    /// 格式化异常为日志消息
    /// </summary>
    /// <param name="ex">异常</param>
    /// <param name="context">上下文信息</param>
    /// <returns>日志消息</returns>
    public static string FormatForLog(Exception ex, string? context = null)
    {
        var message = Format(ex);
        return string.IsNullOrEmpty(context)
            ? message
            : $"[{context}] {message}";
    }

    /// <summary>
    /// 格式化聚合异常
    /// </summary>
    private static string FormatAggregate(System.AggregateException aggEx)
    {
        var messages = aggEx.InnerExceptions
            .Select((innerEx, index) => $"  [{index + 1}] {Format(innerEx)}")
            .ToList();

        return $"多个错误发生:\n{string.Join("\n", messages)}";
    }

    /// <summary>
    /// 获取异常的错误代码
    /// </summary>
    /// <param name="ex">异常</param>
    /// <returns>错误代码</returns>
    public static string GetErrorCode(Exception ex)
    {
        return ex switch
        {
            System.ArgumentNullException => "NULL_ARGUMENT",
            System.ArgumentOutOfRangeException => "OUT_OF_RANGE",
            System.ArgumentException => "ARGUMENT_ERROR",
            System.InvalidOperationException => "INVALID_OPERATION",
            System.Collections.Generic.KeyNotFoundException => "NOT_FOUND",
            System.TimeoutException => "TIMEOUT",
            System.IO.IOException => "IO_ERROR",
            System.UnauthorizedAccessException => "UNAUTHORIZED",
            System.Security.SecurityException => "SECURITY_ERROR",
            System.Text.Json.JsonException => "JSON_ERROR",
            System.FormatException => "FORMAT_ERROR",
            System.NotSupportedException => "NOT_SUPPORTED",
            System.NotImplementedException => "NOT_IMPLEMENTED",
            System.Threading.Tasks.TaskCanceledException => "CANCELLED",
            System.OperationCanceledException => "CANCELLED",
            _ => "UNKNOWN_ERROR"
        };
    }

    /// <summary>
    /// 判断异常是否为临时性错误（可重试）
    /// </summary>
    /// <param name="ex">异常</param>
    /// <returns>是否为临时性错误</returns>
    public static bool IsTransient(Exception ex)
    {
        return ex switch
        {
            System.TimeoutException => true,
            System.IO.IOException => true,
            System.Threading.Tasks.TaskCanceledException => true,
            System.OperationCanceledException => true,
            System.Net.Http.HttpRequestException => true,
            _ => false
        };
    }

    /// <summary>
    /// 获取异常的严重程度
    /// </summary>
    /// <param name="ex">异常</param>
    /// <returns>日志级别</returns>
    public static LogLevel GetSeverity(Exception ex)
    {
        return ex switch
        {
            // 先匹配具体的子类
            System.ArgumentNullException => LogLevel.Warn,
            System.ArgumentOutOfRangeException => LogLevel.Warn,
            System.ArgumentException => LogLevel.Warn,
            System.Collections.Generic.KeyNotFoundException => LogLevel.Warn,
            System.TimeoutException => LogLevel.Warn,
            System.Threading.Tasks.TaskCanceledException => LogLevel.Info,
            System.OperationCanceledException => LogLevel.Info,
            System.UnauthorizedAccessException => LogLevel.Error,
            System.Security.SecurityException => LogLevel.Error,
            _ => LogLevel.Error
        };
    }
}
