namespace Common.Contracts.Attributes;

/// <summary>
/// CLI 名称特性 - 用于将 C# 标识符映射到小写下划线格式的 CLI 名称
/// AOT 兼容：通过 GetCustomAttributes<T>() 读取，不使用 Reflection.Emit
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class CliNameAttribute : Attribute
{
    /// <summary>
    /// CLI 名称（小写下划线格式，如 "memory_cli"、"create_entities"）
    /// </summary>
    public string Name { get; }

    public CliNameAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
}
