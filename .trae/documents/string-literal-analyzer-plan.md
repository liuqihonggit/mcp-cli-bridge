# 字符串分析与变换功能实现计划

## 需求理解

添加一个功能，能够：
1. **分析** C# 源代码中的所有字符串字面量（string literal）
2. **支持** C# 的所有字符串模式（`""`、`@""`、`"""`、`$""`、`$@""`、`$"""`）
3. **变换** 每个字符串的值，添加前缀（prefix）和后缀（suffix）

## 技术方案：基于 Roslyn 的 AST 分析与变换

### 为什么选择 Roslyn？

| 方案 | 优点 | 缺点 |
|------|------|------|
| **正则表达式** | 简单 | 无法区分代码/注释中的字符串；raw string 无法匹配；插值字符串嵌套复杂 |
| **Roslyn AST** ✅ | 精确识别所有字符串类型；自动排除注释；支持插值字符串；项目已有基础设施 | 依赖较重（但 AstCli 已引入） |

AstCli 插件已有完整的 Roslyn 基础设施（`Microsoft.CodeAnalysis.CSharp 4.11.0`），且已有 `CSharpSyntaxRewriter` 的使用先例（`SymbolRenameRewriter`），因此直接在 AstCli 中扩展是最自然的选择。

### C# 字符串字面量的 6 种类型

| 类型 | 语法示例 | Roslyn 节点类型 | 说明 |
|------|---------|----------------|------|
| 常规字符串 | `"hello"` | `LiteralExpressionSyntax` + `StringLiteralToken` | 支持转义序列 `\n`, `\t` 等 |
| 逐字字符串 | `@"hello"` | `LiteralExpressionSyntax` + `StringLiteralToken` | `@` 前缀，`""` 表示引号 |
| 原始字符串 | `"""hello"""` | `LiteralExpressionSyntax` + `StringLiteralToken` | C# 11+，可跨行 |
| 插值字符串 | `$"hello {name}"` | `InterpolatedStringExpressionSyntax` | 包含文本部分和插值部分 |
| 逐字插值字符串 | `$@"hello {name}"` | `InterpolatedStringExpressionSyntax` | `$@` 前缀 |
| 插值原始字符串 | `$"""hello {name}"""` | `InterpolatedStringExpressionSyntax` | C# 11+ |

### 变换策略

**常规/逐字/原始字符串**：
- `"hello"` → `"PREFIX_hello_SUFFIX"` — 修改 Token 的值，保持原有格式
- `@"hello"` → `@"PREFIX_hello_SUFFIX"` — 保持 `@` 前缀
- `"""hello"""` → `"""PREFIX_hello_SUFFIX"""` — 保持 `"""` 分隔符

**插值字符串**：
- `$"hello {name} world"` → `$"PREFIX_hello {name} world_SUFFIX"` — 前缀加到第一个文本部分，后缀加到最后一个文本部分

**跳过的情况**：
- `nameof()` 表达式 — 不是字符串字面量
- `char` 字面量（`'a'`）— 不是字符串
- `typeof()` 表达式 — 不是字符串字面量

## 实现步骤

### Step 1: 添加 DTO 模型

在 [AstCliModels.cs](file:///g:/Project/AI相关/McpHost/src/Plugins/AstCli/Models/AstCliModels.cs) 中添加：

```csharp
public sealed class StringLiteralInfoDto
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;  // Regular, Verbatim, Raw, Interpolated, VerbatimInterpolated, InterpolatedRaw

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("column")]
    public int Column { get; set; }

    [JsonPropertyName("context")]
    public string? Context { get; set; }  // 所在行的代码片段
}

public sealed class StringAnalyzeResultDto
{
    [JsonPropertyName("projectPath")]
    public string ProjectPath { get; set; } = string.Empty;

    [JsonPropertyName("strings")]
    public List<StringLiteralInfoDto> Strings { get; set; } = [];

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("countByKind")]
    public Dictionary<string, int> CountByKind { get; set; } = [];
}

public sealed class StringTransformResultDto
{
    [JsonPropertyName("prefix")]
    public string Prefix { get; set; } = string.Empty;

    [JsonPropertyName("suffix")]
    public string Suffix { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("transformedCount")]
    public int TransformedCount { get; set; }

    [JsonPropertyName("modifiedFiles")]
    public List<string> ModifiedFiles { get; set; } = [];

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
```

在 `AstCliRequest` 中添加新字段：

```csharp
[JsonPropertyName("prefix")]
public string? Prefix { get; set; }

[JsonPropertyName("suffix")]
public string? Suffix { get; set; }

[JsonPropertyName("filter")]
public string? Filter { get; set; }  // 可选：按值过滤字符串

[JsonPropertyName("dryRun")]
public bool DryRun { get; set; }  // 预览模式，不实际修改文件
```

### Step 2: 更新 JSON 序列化上下文

在 [AstCliJsonContext.cs](file:///g:/Project/AI相关/McpHost/src/Plugins/AstCli/Models/AstCliJsonContext.cs) 中添加：

```csharp
[JsonSerializable(typeof(StringLiteralInfoDto))]
[JsonSerializable(typeof(StringAnalyzeResultDto))]
[JsonSerializable(typeof(StringTransformResultDto))]
[JsonSerializable(typeof(List<StringLiteralInfoDto>))]
[JsonSerializable(typeof(Dictionary<string, int>))]
```

### Step 3: 创建 StringLiteralEngine

新建 `src/Plugins/AstCli/Services/StringLiteralEngine.cs`：

核心逻辑：

```csharp
namespace AstCli.Services;

public sealed class StringLiteralEngine
{
    /// <summary>
    /// 分析项目中的所有字符串字面量
    /// </summary>
    public static async Task<StringAnalyzeResultDto> AnalyzeAsync(
        string projectPath, string? filePath, string? filter)

    /// <summary>
    /// 变换项目中的字符串字面量（添加前缀/后缀）
    /// </summary>
    public static async Task<StringTransformResultDto> TransformAsync(
        string projectPath, string? filePath, string prefix, string suffix,
        string? filter, bool dryRun)
}

// CSharpSyntaxRewriter 用于变换字符串
file sealed class StringLiteralRewriter : CSharpSyntaxRewriter
{
    // VisitLiteralExpression — 处理常规/逐字/原始字符串
    // VisitInterpolatedStringExpression — 处理插值字符串
}

// CSharpSyntaxWalker 用于收集字符串信息
file sealed class StringLiteralCollector : CSharpSyntaxWalker
{
    // VisitLiteralExpression — 收集字符串信息
    // VisitInterpolatedStringExpression — 收集插值字符串信息
}
```

**字符串类型判断逻辑**：

```csharp
private static string GetStringKind(SyntaxToken token)
{
    var text = token.Text;
    if (text.StartsWith("$@\"", StringComparison.Ordinal) || text.StartsWith("@$\"", StringComparison.Ordinal))
        return "VerbatimInterpolated";  // 不会在 LiteralExpression 中出现，在 InterpolatedStringExpression 中
    if (text.StartsWith("$\"\"\"", StringComparison.Ordinal))
        return "InterpolatedRaw";       // 同上
    if (text.StartsWith("\"\"\"", StringComparison.Ordinal))
        return "Raw";
    if (text.StartsWith("@\"", StringComparison.Ordinal))
        return "Verbatim";
    return "Regular";
}
```

**变换核心逻辑**：

对于 `LiteralExpressionSyntax`（常规/逐字/原始字符串）：
1. 获取原始值：`node.Token.ValueText`
2. 检查是否匹配 filter（如果有）
3. 构造新值：`prefix + originalValue + suffix`
4. 根据字符串类型创建新 Token：
   - Regular: `SyntaxFactory.Literal(prefix + originalValue + suffix)`
   - Verbatim: `SyntaxFactory.Literal(...)` 但需要处理 `""` 转义
   - Raw: 修改 `"""` 之间的内容

对于 `InterpolatedStringExpressionSyntax`（插值字符串）：
1. 找到第一个 `InterpolatedStringTextPartSyntax`，在其文本前添加 prefix
2. 找到最后一个 `InterpolatedStringTextPartSyntax`，在其文本后添加 suffix
3. 如果没有文本部分（如 `$"{name}"`），在开头/结尾插入新的文本部分

### Step 4: 添加 Schema 模板

在 [AstToolSchemaTemplates.cs](file:///g:/Project/AI相关/McpHost/src/Plugins/AstCli/Schemas/AstToolSchemaTemplates.cs) 中添加：

```csharp
public static JsonElement StringAnalyzeSchema() { ... }
public static JsonElement StringTransformSchema() { ... }
```

### Step 5: 添加命令处理

在 [CommandHandler.cs](file:///g:/Project/AI相关/McpHost/src/Plugins/AstCli/Commands/CommandHandler.cs) 中添加：

```csharp
"string_analyze" => await StringAnalyzeAsync(request),
"string_transform" => await StringTransformAsync(request),
```

以及对应的处理方法。

更新 `ListTools()` 中的 `CommandCount = 11`（原 9 + 新增 2）。

更新 `ListCommands()` 添加两个新工具定义。

### Step 6: 单元测试

在 `tests/UnitTests/` 中添加 `StringLiteralEngineTests.cs`，覆盖：

1. **分析测试**：
   - 常规字符串识别
   - 逐字字符串识别
   - 原始字符串识别
   - 插值字符串识别
   - 过滤功能
   - 排除注释中的"字符串"

2. **变换测试**：
   - 常规字符串添加前缀/后缀
   - 逐字字符串添加前缀/后缀
   - 原始字符串添加前缀/后缀
   - 插值字符串添加前缀/后缀
   - DryRun 模式不修改文件
   - 过滤功能

### Step 7: 编译验证

```powershell
dotnet build McpHost.slnx -c Release
dotnet test McpHost.slnx -c Release
```

## 文件变更清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `src/Plugins/AstCli/Models/AstCliModels.cs` | 修改 | 添加 3 个 DTO + AstCliRequest 新字段 |
| `src/Plugins/AstCli/Models/AstCliJsonContext.cs` | 修改 | 添加 5 个 `[JsonSerializable]` |
| `src/Plugins/AstCli/Services/StringLiteralEngine.cs` | 新建 | 字符串分析与变换引擎 |
| `src/Plugins/AstCli/Schemas/AstToolSchemaTemplates.cs` | 修改 | 添加 2 个 Schema 方法 |
| `src/Plugins/AstCli/Commands/CommandHandler.cs` | 修改 | 添加 2 个命令处理 + 更新工具列表 |
| `tests/UnitTests/StringLiteralEngineTests.cs` | 新建 | 单元测试 |

## 自主决策记录

- **决策**: 将功能放在 AstCli 插件中而非新建项目
- **原因**: AstCli 已有 Roslyn 基础设施和 CSharpSyntaxRewriter 使用先例，避免重复引入依赖
- **替代方案**: 新建独立 CLI 工具（增加维护成本，且 Roslyn 依赖较重不宜放入 Common）
- **决策**: 使用 CSharpSyntaxWalker 收集信息 + CSharpSyntaxRewriter 变换代码
- **原因**: 遵循 AstEngine 的既有模式，Walker 只读遍历，Rewriter 可变换节点
