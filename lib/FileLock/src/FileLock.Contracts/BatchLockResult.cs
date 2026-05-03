namespace FileLock.Contracts;

public sealed class BatchLockResult
{
    public bool Success { get; init; }
    public IBatchLock? BatchLock { get; init; }
    public string? FailedFile { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan WaitTime { get; init; }

    public static BatchLockResult SuccessResult(IBatchLock batchLock, TimeSpan waitTime) => new()
    {
        Success = true,
        BatchLock = batchLock,
        WaitTime = waitTime
    };

    public static BatchLockResult TimeoutResult(string failedFile, TimeSpan waitTime) => new()
    {
        Success = false,
        FailedFile = failedFile,
        ErrorMessage = $"Lock acquisition timed out on '{failedFile}'",
        WaitTime = waitTime
    };

    public static BatchLockResult ErrorResult(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}
