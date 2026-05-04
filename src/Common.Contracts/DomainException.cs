namespace Common.Contracts;

public abstract class DomainException : Exception
{
    public string ErrorCode { get; }

    protected DomainException()
    {
        ErrorCode = "UNKNOWN_ERROR";
    }

    protected DomainException(string message)
        : base(message)
    {
        ErrorCode = "UNKNOWN_ERROR";
    }

    protected DomainException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = "UNKNOWN_ERROR";
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

    public ValidationException()
        : base("VALIDATION_ERROR", "Validation failed")
    {
        Errors = Array.Empty<string>();
    }

    public ValidationException(string message)
        : base("VALIDATION_ERROR", message)
    {
        Errors = Array.Empty<string>();
    }

    public ValidationException(IEnumerable<string> errors)
        : base("VALIDATION_ERROR", string.Join("; ", errors))
    {
        Errors = errors.ToList().AsReadOnly();
    }

    public ValidationException(string message, Exception innerException)
        : base("VALIDATION_ERROR", message, innerException)
    {
        Errors = Array.Empty<string>();
    }
}

public sealed class EntityNotFoundException : DomainException
{
    public EntityNotFoundException()
        : base("ENTITY_NOT_FOUND", "Entity not found")
    {
    }

    public EntityNotFoundException(string entityName)
        : base("ENTITY_NOT_FOUND", $"Entity not found: {entityName}")
    {
    }

    public EntityNotFoundException(string message, Exception innerException)
        : base("ENTITY_NOT_FOUND", message, innerException)
    {
    }
}

public sealed class LockTimeoutException : DomainException
{
    public LockTimeoutException()
        : base("LOCK_TIMEOUT", "Lock timeout occurred")
    {
    }

    public LockTimeoutException(string resource)
        : base("LOCK_TIMEOUT", $"Lock timeout while accessing: {resource}")
    {
    }

    public LockTimeoutException(string message, Exception innerException)
        : base("LOCK_TIMEOUT", message, innerException)
    {
    }
}

public sealed class ToolNotFoundException : DomainException
{
    public ToolNotFoundException()
        : base("TOOL_NOT_FOUND", "Tool not found")
    {
    }

    public ToolNotFoundException(string toolName)
        : base("TOOL_NOT_FOUND", $"Tool not found: {toolName}")
    {
    }

    public ToolNotFoundException(string message, Exception innerException)
        : base("TOOL_NOT_FOUND", message, innerException)
    {
    }
}
