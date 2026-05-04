using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.Configuration;

/// <summary>
/// 选项工具类，提供非泛型的静态辅助方法
/// </summary>
public static class OptionsHelper
{
    /// <summary>
    /// JSON序列化选项（AOT兼容）
    /// </summary>
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    /// 使用配置器创建选项
    /// </summary>
    internal static T Create<T>(Action<T> configure) where T : new()
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new T();
        configure(options);
        return options;
    }

    /// <summary>
    /// 从JSON字符串反序列化选项
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Options types are AOT-compatible with source generation")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Options types are trimming-safe with source generation")]
    internal static T FromJson<T>(string json) where T : new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var result = JsonSerializer.Deserialize<T>(json, JsonOptions);
        return result ?? throw new InvalidOperationException($"无法将JSON反序列化为 {typeof(T).Name}");
    }

    /// <summary>
    /// 从JSON字符串反序列化选项，失败时返回默认值
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Options types are AOT-compatible with source generation")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Options types are trimming-safe with source generation")]
    internal static T FromJsonOrDefault<T>(string json, T defaultValue) where T : new()
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return defaultValue;
        }

        try
        {
            var result = JsonSerializer.Deserialize<T>(json, JsonOptions);
            return result ?? defaultValue;
        }
        catch (JsonException)
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 克隆选项（使用JSON序列化实现深拷贝）
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Options types are AOT-compatible")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Options types are trimming-safe")]
    internal static T Clone<T>(T instance) where T : new()
    {
        var json = JsonSerializer.Serialize(instance, JsonOptions);
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new T();
    }

    /// <summary>
    /// 将选项序列化为JSON字符串
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Options types are AOT-compatible")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Options types are trimming-safe")]
    internal static string ToJson<T>(T instance)
    {
        return JsonSerializer.Serialize(instance, JsonOptions);
    }
}

/// <summary>
/// 选项创建器，提供非泛型的静态工厂方法
/// </summary>
public static class Options
{
    /// <summary>
    /// 创建默认选项实例
    /// </summary>
    public static T Default<T>() where T : new() => new();

    /// <summary>
    /// 使用配置器创建选项
    /// </summary>
    public static T Create<T>(Action<T> configure) where T : new()
    {
        return OptionsHelper.Create(configure);
    }

    /// <summary>
    /// 从JSON字符串反序列化选项
    /// </summary>
    public static T FromJson<T>(string json) where T : new()
    {
        return OptionsHelper.FromJson<T>(json);
    }

    /// <summary>
    /// 从JSON字符串反序列化选项，失败时返回默认值
    /// </summary>
    public static T FromJsonOrDefault<T>(string json) where T : new()
    {
        return OptionsHelper.FromJsonOrDefault(json, new T());
    }

    /// <summary>
    /// 从JSON字符串反序列化选项，失败时返回默认值
    /// </summary>
    public static T FromJsonOrDefault<T>(string json, T defaultValue) where T : new()
    {
        return OptionsHelper.FromJsonOrDefault(json, defaultValue);
    }
}

/// <summary>
/// 配置选项基类，提供通用的工厂方法和配置模式
/// </summary>
/// <typeparam name="T">选项类型</typeparam>
public abstract class OptionsBase<T> where T : OptionsBase<T>, new()
{
    /// <summary>
    /// JSON序列化选项（AOT兼容）
    /// </summary>
    protected static JsonSerializerOptions JsonOptions => OptionsHelper.JsonOptions;

    /// <summary>
    /// 从现有选项创建副本并配置
    /// </summary>
    /// <param name="configure">配置操作</param>
    /// <returns>配置后的新选项</returns>
    public T With(Action<T> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = Clone();
        configure(options);
        return options;
    }

    /// <summary>
    /// 克隆当前选项（使用JSON序列化实现深拷贝）
    /// </summary>
    /// <returns>选项副本</returns>
    protected virtual T Clone()
    {
        return OptionsHelper.Clone((T)this);
    }

    /// <summary>
    /// 将选项序列化为JSON字符串
    /// </summary>
    /// <returns>JSON字符串</returns>
    public string ToJson()
    {
        return OptionsHelper.ToJson((T)this);
    }
}

/// <summary>
/// 预设工厂接口
/// </summary>
/// <typeparam name="T">选项类型</typeparam>
public interface IOptionsPresetFactory<T> where T : OptionsBase<T>, new()
{
    /// <summary>
    /// 创建小型配置预设
    /// </summary>
    T CreateSmall();

    /// <summary>
    /// 创建大型配置预设
    /// </summary>
    T CreateLarge();
}

/// <summary>
/// 带预设的配置选项基类
/// </summary>
/// <typeparam name="T">选项类型</typeparam>
public abstract class OptionsBaseWithPresets<T> : OptionsBase<T> where T : OptionsBaseWithPresets<T>, new()
{
    /// <summary>
    /// 获取预设工厂
    /// </summary>
    public abstract IOptionsPresetFactory<T> GetPresetFactory();
}

/// <summary>
/// 预设选项访问器
/// </summary>
public static class OptionsPresetsAccessor
{
    /// <summary>
    /// 获取小型配置预设
    /// </summary>
    public static T Small<T>() where T : OptionsBaseWithPresets<T>, new()
    {
        return OptionsPresetHelper<T>.GetSmall();
    }

    /// <summary>
    /// 获取大型配置预设
    /// </summary>
    public static T Large<T>() where T : OptionsBaseWithPresets<T>, new()
    {
        return OptionsPresetHelper<T>.GetLarge();
    }
}

/// <summary>
/// 预设帮助类，用于存储泛型类型的静态实例
/// </summary>
/// <typeparam name="T">选项类型</typeparam>
internal static class OptionsPresetHelper<T> where T : OptionsBaseWithPresets<T>, new()
{
    private static readonly Lazy<T> SmallLazy = new(() =>
    {
        var instance = new T();
        return instance.GetPresetFactory().CreateSmall();
    });

    private static readonly Lazy<T> LargeLazy = new(() =>
    {
        var instance = new T();
        return instance.GetPresetFactory().CreateLarge();
    });

    internal static T GetSmall() => SmallLazy.Value;
    internal static T GetLarge() => LargeLazy.Value;
}

/// <summary>
/// 带验证的配置选项基类
/// </summary>
/// <typeparam name="T">选项类型</typeparam>
public abstract class ValidatableOptionsBase<T> : OptionsBase<T> where T : ValidatableOptionsBase<T>, new()
{
    /// <summary>
    /// 验证选项是否有效
    /// </summary>
    /// <returns>验证结果</returns>
    public abstract OptionsValidationResult Validate();

    /// <summary>
    /// 验证并抛出异常（如果无效）
    /// </summary>
    /// <exception cref="OptionsValidationException">验证失败时抛出</exception>
    public void ValidateAndThrow()
    {
        var result = Validate();
        if (!result.IsValid)
        {
            throw new OptionsValidationException(result.Errors);
        }
    }
}

/// <summary>
/// 可验证选项帮助类，提供非泛型的静态工厂方法
/// </summary>
public static class ValidatableOptions
{
    /// <summary>
    /// 创建并验证选项
    /// </summary>
    /// <typeparam name="T">选项类型</typeparam>
    /// <param name="configure">配置操作</param>
    /// <returns>验证后的选项</returns>
    /// <exception cref="OptionsValidationException">验证失败时抛出</exception>
    public static T CreateValidated<T>(Action<T> configure) where T : ValidatableOptionsBase<T>, new()
    {
        var options = OptionsHelper.Create(configure);
        options.ValidateAndThrow();
        return options;
    }
}

/// <summary>
/// 选项验证结果
/// </summary>
public sealed class OptionsValidationResult
{
    private readonly List<string> _errors = [];
    private readonly List<string> _warnings = [];

    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid => _errors.Count == 0;

    /// <summary>
    /// 错误列表
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    /// 警告列表
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    /// 添加错误
    /// </summary>
    public void AddError(string error) => _errors.Add(error);

    /// <summary>
    /// 添加警告
    /// </summary>
    public void AddWarning(string warning) => _warnings.Add(warning);

    /// <summary>
    /// 合并另一个验证结果
    /// </summary>
    public void Merge(OptionsValidationResult other)
    {
        ArgumentNullException.ThrowIfNull(other);

        _errors.AddRange(other._errors);
        _warnings.AddRange(other._warnings);
    }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static OptionsValidationResult Success() => new();

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static OptionsValidationResult Failed(params string[] errors)
    {
        var result = new OptionsValidationResult();
        foreach (var error in errors)
        {
            result.AddError(error);
        }
        return result;
    }

    /// <summary>
    /// 创建包含警告的成功结果
    /// </summary>
    public static OptionsValidationResult WithWarnings(params string[] warnings)
    {
        var result = new OptionsValidationResult();
        foreach (var warning in warnings)
        {
            result.AddWarning(warning);
        }
        return result;
    }
}

/// <summary>
/// 选项验证异常
/// </summary>
public sealed class OptionsValidationException : Exception
{
    /// <summary>
    /// 错误列表
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    public OptionsValidationException(IReadOnlyList<string> errors)
        : base($"配置验证失败: {string.Join(", ", errors)}")
    {
        Errors = errors;
    }
}

/// <summary>
/// 预设配置委托
/// </summary>
/// <typeparam name="T">选项类型</typeparam>
public delegate T OptionsPreset<T>() where T : OptionsBase<T>, new();

/// <summary>
/// 预设配置注册表
/// </summary>
public static class OptionsPresets
{
    private static readonly Dictionary<Type, Dictionary<string, Delegate>> _presets = new();

    /// <summary>
    /// 注册预设配置
    /// </summary>
    /// <typeparam name="T">选项类型</typeparam>
    /// <param name="name">预设名称</param>
    /// <param name="factory">预设工厂</param>
    public static void Register<T>(string name, OptionsPreset<T> factory) where T : OptionsBase<T>, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(factory);

        var type = typeof(T);
        if (!_presets.TryGetValue(type, out var typePresets))
        {
            typePresets = new Dictionary<string, Delegate>(StringComparer.OrdinalIgnoreCase);
            _presets[type] = typePresets;
        }

        typePresets[name] = factory;
    }

    /// <summary>
    /// 获取预设配置
    /// </summary>
    /// <typeparam name="T">选项类型</typeparam>
    /// <param name="name">预设名称</param>
    /// <returns>预设选项</returns>
    /// <exception cref="KeyNotFoundException">预设不存在时抛出</exception>
    public static T Get<T>(string name) where T : OptionsBase<T>, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var type = typeof(T);
        if (_presets.TryGetValue(type, out var typePresets) &&
            typePresets.TryGetValue(name, out var factory))
        {
            return ((OptionsPreset<T>)factory)();
        }

        throw new KeyNotFoundException($"未找到 {type.Name} 的预设 '{name}'");
    }

    /// <summary>
    /// 尝试获取预设配置
    /// </summary>
    /// <typeparam name="T">选项类型</typeparam>
    /// <param name="name">预设名称</param>
    /// <param name="options">预设选项</param>
    /// <returns>是否成功获取</returns>
    public static bool TryGet<T>(string name, out T? options) where T : OptionsBase<T>, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var type = typeof(T);
        if (_presets.TryGetValue(type, out var typePresets) &&
            typePresets.TryGetValue(name, out var factory))
        {
            options = ((OptionsPreset<T>)factory)();
            return true;
        }

        options = null;
        return false;
    }

    /// <summary>
    /// 获取所有预设名称
    /// </summary>
    /// <typeparam name="T">选项类型</typeparam>
    /// <returns>预设名称列表</returns>
    public static IReadOnlyList<string> GetPresetNames<T>() where T : OptionsBase<T>, new()
    {
        var type = typeof(T);
        if (_presets.TryGetValue(type, out var typePresets))
        {
            return typePresets.Keys.ToList();
        }

        return Array.Empty<string>();
    }
}
