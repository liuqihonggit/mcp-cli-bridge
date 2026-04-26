using Common.Contracts;

namespace Common.Results;

/// <summary>
/// OperationResult 工厂类，用于创建各种操作结果
/// </summary>
public static class OperationResultFactory
{
    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static OperationResult<T> Ok<T>(T data, string message = "")
    {
        return new OperationResult<T>
        {
            Success = true,
            Message = message,
            Data = data,
            ExitCode = 0
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static OperationResult<T> Fail<T>(string message)
    {
        return new OperationResult<T>
        {
            Success = false,
            Message = message,
            Data = default,
            ExitCode = -1
        };
    }

    /// <summary>
    /// 从异常创建失败结果
    /// </summary>
    public static OperationResult<T> FromException<T>(Exception exception)
    {
        return new OperationResult<T>
        {
            Success = false,
            Message = exception.Message,
            Data = default,
            ExitCode = -1,
            Error = exception.Message
        };
    }

    /// <summary>
    /// 创建CLI执行成功结果
    /// </summary>
    public static OperationResult<T> CliSuccess<T>(T data, string output, int exitCode = 0, double executionTimeMs = 0)
    {
        return new OperationResult<T>
        {
            Success = true,
            Message = output,
            Data = data,
            ExitCode = exitCode,
            ExecutionTimeMs = executionTimeMs
        };
    }

    /// <summary>
    /// 创建CLI执行失败结果
    /// </summary>
    public static OperationResult<T> CliFailure<T>(string error, int exitCode = -1, double executionTimeMs = 0)
    {
        return new OperationResult<T>
        {
            Success = false,
            Message = error,
            Data = default,
            ExitCode = exitCode,
            ExecutionTimeMs = executionTimeMs,
            Error = error
        };
    }

    /// <summary>
    /// 创建取消结果
    /// </summary>
    public static OperationResult<T> Cancelled<T>(double executionTimeMs = 0)
    {
        return new OperationResult<T>
        {
            Success = false,
            Message = "Execution was cancelled.",
            Data = default,
            ExitCode = -1,
            ExecutionTimeMs = executionTimeMs,
            Error = "Execution was cancelled."
        };
    }

    /// <summary>
    /// 创建超时结果
    /// </summary>
    public static OperationResult<T> Timeout<T>(TimeSpan timeout, double executionTimeMs = 0)
    {
        var message = $"Command timed out after {timeout.TotalSeconds} seconds";
        return new OperationResult<T>
        {
            Success = false,
            Message = message,
            Data = default,
            ExitCode = -1,
            ExecutionTimeMs = executionTimeMs,
            Error = message
        };
    }
}

/// <summary>
/// 非泛型 OperationResult 工厂类
/// </summary>
public static class OperationResultFactoryNonGeneric
{
    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static OperationResult Ok(string message = "")
    {
        return new OperationResult
        {
            Success = true,
            Message = message,
            ExitCode = 0
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static OperationResult Fail(string message)
    {
        return new OperationResult
        {
            Success = false,
            Message = message,
            ExitCode = -1
        };
    }

    /// <summary>
    /// 创建带元数据的结果
    /// </summary>
    public static OperationResult WithMetadata(bool success, string message, Dictionary<string, object> metadata)
    {
        return new OperationResult
        {
            Success = success,
            Message = message,
            Metadata = metadata,
            ExitCode = success ? 0 : -1
        };
    }

    /// <summary>
    /// 创建CLI执行成功结果
    /// </summary>
    public static OperationResult CliSuccess(string output, int exitCode = 0, double executionTimeMs = 0)
    {
        return new OperationResult
        {
            Success = true,
            Message = output,
            ExitCode = exitCode,
            ExecutionTimeMs = executionTimeMs
        };
    }

    /// <summary>
    /// 创建CLI执行失败结果
    /// </summary>
    public static OperationResult CliFailure(string error, int exitCode = -1, double executionTimeMs = 0)
    {
        return new OperationResult
        {
            Success = false,
            Message = error,
            ExitCode = exitCode,
            ExecutionTimeMs = executionTimeMs,
            Error = error
        };
    }

    /// <summary>
    /// 从异常创建失败结果
    /// </summary>
    public static OperationResult FromException(Exception exception, double executionTimeMs = 0)
    {
        return new OperationResult
        {
            Success = false,
            Message = exception.Message,
            ExitCode = -1,
            ExecutionTimeMs = executionTimeMs,
            Error = exception.Message
        };
    }

    /// <summary>
    /// 创建取消结果
    /// </summary>
    public static OperationResult Cancelled(double executionTimeMs = 0)
    {
        return new OperationResult
        {
            Success = false,
            Message = "Execution was cancelled.",
            ExitCode = -1,
            ExecutionTimeMs = executionTimeMs,
            Error = "Execution was cancelled."
        };
    }

    /// <summary>
    /// 创建超时结果
    /// </summary>
    public static OperationResult Timeout(TimeSpan timeout, double executionTimeMs = 0)
    {
        var message = $"Command timed out after {timeout.TotalSeconds} seconds";
        return new OperationResult
        {
            Success = false,
            Message = message,
            ExitCode = -1,
            ExecutionTimeMs = executionTimeMs,
            Error = message
        };
    }
}
