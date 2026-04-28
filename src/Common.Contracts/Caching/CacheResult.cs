namespace Common.Contracts.Caching;

public readonly struct CacheResult<T>
{
    public bool IsHit { get; init; }
    public T? Value { get; init; }
    
    public static CacheResult<T> Hit(T value) => new()
    {
        IsHit = true,
        Value = value
    };
    
    public static CacheResult<T> Miss => new() { IsHit = false };
}
