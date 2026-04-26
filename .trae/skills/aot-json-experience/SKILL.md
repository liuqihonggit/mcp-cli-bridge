# AOT 与 JSON 序列化经验总结

## 成功经验

### 1. JsonSerializerContext 源生成器
- .NET NativeAOT 编译必须使用 `JsonSerializerContext` 源生成器
- 不能使用运行时 `JsonSerializer.Serialize<T>` 或 `JsonSerializer.Deserialize<T>`
- 每个项目/模块应创建独立的 `JsonSerializerContext`，避免类型冲突

```csharp
[JsonSerializable(typeof(MyType))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class MyContext : JsonSerializerContext { }
```

### 2. 显式声明所有序列化类型
- `JsonSerializerContext` 必须显式声明所有序列化类型
- 包括嵌套类型如 `Dictionary<string, JsonElement>`
- 使用 `JsonPropertyName` 特性显式指定 JSON 属性名

### 3. Task<T> 结果获取
- AOT 下 `Task<T>.Result` 属性可能被修剪
- 应使用模式匹配：`task is Task<string> stringTask`
- 避免使用反射获取 `Result` 属性

```csharp
private static string GetTaskResult(Task task)
{
    if (task is Task<string> stringTask)
        return stringTask.Result;
    return string.Empty;
}
```

### 4. 单文件发布路径处理
- `AppContext.BaseDirectory` 在单文件发布时指向临时目录
- 应使用 `Process.GetCurrentProcess().MainModule?.FileName` 获取实际路径

```csharp
var processDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName);
```

### 5. DynamicallyAccessedMembers 注解
- 必须应用于泛型参数、属性、字段等所有相关位置
- 接口和实现类的泛型注解必须一致

```csharp
public void AddSingleton<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
```

## 避坑指南

### 1. record struct 与 DynamicallyAccessedMembers
- `record struct` 的自动属性与 `DynamicallyAccessedMembers` 注解不兼容
- 应使用手动定义的结构体

```csharp
// 错误
private readonly record struct ServiceRegistration(
    Type ServiceType,
    [property: DynamicallyAccessedMembers(...)] Type? ImplementationType,
    ...
);

// 正确
private readonly struct ServiceRegistration
{
    public Type ServiceType { get; }
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type? ImplementationType { get; }
    ...
}
```

### 2. 避免动态类型序列化
- `JsonSerializer.Serialize(object)` 在 AOT 下会报错
- 必须使用强类型重载或源生成器
- 避免序列化 `Dictionary<string, object>` 等动态类型

### 3. 反射获取属性
- `Type.GetProperty("Result")` 在 AOT 下可能返回 null
- 应使用模式匹配或编译时已知类型

### 4. 中间件中的 JSON 序列化
- 不要在中间件或通用工具类中使用 `JsonSerializer.Serialize<TValue>(value)`
- 这会导致 AOT 不兼容
- 应使用源生成器上下文

## 项目配置

### Directory.Build.props
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <PublishAot>true</PublishAot>
  <TrimMode>full</TrimMode>
  <InvariantGlobalization>true</InvariantGlobalization>
  <StackTraceSupport>false</StackTraceSupport>
</PropertyGroup>
```

## 验证命令

```powershell
# 发布并检查 AOT 兼容性
dotnet publish -c Release -o publish --self-contained false

# 运行 E2E 测试
dotnet run --project tests\E2E\MyMemoryServer.E2E.csproj
```
