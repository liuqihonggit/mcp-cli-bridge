namespace Common.Contracts;

public abstract class DomainException : Exception
{
    public string ErrorCode { get; }

    protected DomainException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    protected DomainException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

public sealed class ValidationException : DomainException
{
    public IReadOnlyList<string> Errors { get; }

    public ValidationException(IEnumerable<string> errors)
        : base("VALIDATION_ERROR", string.Join("; ", errors))
    {
        Errors = errors.ToList().AsReadOnly();
    }
}

public sealed class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName)
        : base("ENTITY_NOT_FOUND", $"Entity not found: {entityName}")
    {
    }
}

public sealed class LockTimeoutException : DomainException
{
    public LockTimeoutException(string resource)
        : base("LOCK_TIMEOUT", $"Lock timeout while accessing: {resource}")
    {
    }
}

public sealed class ToolNotFoundException : DomainException
{
    public ToolNotFoundException(string toolName)
        : base("TOOL_NOT_FOUND", $"Tool not found: {toolName}")
    {
    }
}
