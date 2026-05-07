# 字符串分析与变换功能实现计划

## 需求理解

在 AstCli 插件中添加 4 个 AST 命令，核心操作是**在字符串的指定位置插入文本**：

| 命令              | 功能                | 位置   |
| --------------- | ----------------- | ---- |
| `string_query`  | 查询字符串字面量（支持按前缀过滤） | —    |
| `string_prefix` | 写入前缀              | 0 位置 |
| `string_suffix` | 写入后缀              | 末尾位置 |
| `string_insert` | 中间插入              | 任意位置 |

三个变换命令共享同一个核心操作：**在字符串值的位置 N 插入文本**。

* `string_prefix` = position=0 的特例

* `string_suffix` = position=原字符串长度 的特例

* `string_insert` = position=任意值

## 技术方案：基于 Roslyn 的 AST 分析与变换

### 为什么选择 Roslyn？

| 方案               | 优点                                  | 缺点                                       |
| ---------------- | ----------------------------------- | ---------------------------------------- |
| **正则表达式**        | 简单                                  | 无法区分代码/注释中的字符串；raw string 无法匹配；插值字符串嵌套复杂 |
| **Roslyn AST** ✅ | 精确识别所有字符串类型；自动排除注释；支持插值字符串；项目已有基础设施 | 依赖较重（但 AstCli 已引入）                       |

AstCli 插件已有完整的 Roslyn 基础设施（`Microsoft.CodeAnalysis.CSharp 4.11.0`），且已有 `CSharpSyntaxRewriter` 的使用先例（`SymbolRenameRewriter`），因此直接在 AstCli 中扩展是最自然的选择。

### C# 字符串字面量的 6 种类型

| 类型      | 语法示例                  | Roslyn 节点类型                                      | 说明                  |
| ------- | --------------------- | ------------------------------------------------ | ------------------- |
| 常规字符串   | `"hello"`             | `LiteralExpressionSyntax` + `StringLiteralToken` | 支持转义序列 `\n`, `\t` 等 |
| 逐字字符串   | `@"hello"`            | `LiteralExpressionSyntax` + `StringLiteralToken` | `@` 前缀，`""` 表示引号    |
| 原始字符串   | `"""hello"""`         | `LiteralExpressionSyntax` + `StringLiteralToken` | C# 11+，可跨行          |
| 插值字符串   | `$"hello {name}"`     | `InterpolatedStringExpressionSyntax`             | 包含文本部分和插值部分         |
| 逐字插值字符串 | `$@"hello {name}"`    | `InterpolatedStringExpressionSyntax`             | `$@` 前缀             |
| 插值原始字符串 | `$"""hello {name}"""` | `InterpolatedStringExpressionSyntax`             | C# 11+              |

### 核心变换策略：在位置 N 插入文本

**常规/逐字/原始字符串**（`LiteralExpressionSyntax`）：

1. 获取原始值：`node.Token.ValueText`
2. 检查是否匹配 filter（如果有）
3. 在位置 N 插入文本：`value[..position] + insertText + value[position..]`
4. 根据字符串类型创建新 Token：

   * Regular: `SyntaxFactory.Literal(newValue)` — 自动处理转义

   * Verbatim: `SyntaxFactory.Literal(...)` 但需要处理 `""` 转义

   * Raw: 修改 `"""` 之间的内容

**插值字符串**（`InterpolatedStringExpressionSyntax`）：

* 需要计算文本部分（`InterpolatedStringTextPartSyntax`）的字符偏移量

* 在对应位置拆分文本部分，插入新文本

* 特殊情况：position=0 时在第一个文本部分前插入；position=末尾时在最后一个文本部分后插入

**跳过的情况**：

* `nameof()` 表达式 — 不是字符串字面量

* `char` 字面量（`'a'`）— 不是字符串

* `typeof()` 表达式 — 不是字符串字面量

### 四个命令的参数设计

**`string_query`** — 查询字符串

```
参数:
  projectPath: string (必填) — 项目路径
  filePath: string? (可选) — 指定文件，不填则扫描整个项目
  prefix: string? (可选) — 按前缀过滤字符串值（如 "MCP" 只返回以 "MCP" 开头的字符串）
  filter: string? (可选) — 按包含内容过滤
  scope: string? (可选) — "project" 或 "file"
```

**`string_prefix`** — 写入前缀（position=0 的特例）

```
参数:
  projectPath: string (必填)
  filePath: string? (可选)
  insertText: string (必填) — 要插入的前缀文本
  filter: string? (可选) — 只变换匹配的字符串
  dryRun: bool (默认 false) — 预览模式，不实际修改文件
```

**`string_suffix`** — 写入后缀（position=末尾 的特例）

```
参数:
  projectPath: string (必填)
  filePath: string? (可选)
  insertText: string (必填) — 要插入的后缀文本
  filter: string? (可选) — 只变换匹配的字符串
  dryRun: bool (默认 false)
```

**`string_insert`** — 中间插入（position=任意值）

```
参数:
  projectPath: string (必填)
  filePath: string? (可选)
  insertText: string (必填) — 要插入的文本
  position: int (必填) — 插入位置（0=开头，等于字符串长度=末尾）
  filter: string? (可选) — 只变换匹配的字符串
  dryRun: bool (默认 false)
```

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
    public string? FilePath { get; set; }

    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("column")]
    public int Column { get; set; }

    [JsonPropertyName("length")]
    public int Length { get; set; }  // 字符串值长度

    [JsonPropertyName("context")]
    public string? Context { get; set; }  // 所在行的代码片段
}

public sealed class StringQueryResultDto
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

public sealed class StringInsertResultDto
{
    [JsonPropertyName("insertText")]
    public string InsertText { get; set; } = string.Empty;

    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;  // "prefix", "suffix", "insert"

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
[JsonPropertyName("insertText")]
public string? InsertText { get; set; }

[JsonPropertyName("position")]
public int Position { get; set; }

[JsonPropertyName("prefix")]
public string? Prefix { get; set; }  // string_query 的前缀过滤参数

[JsonPropertyName("filter")]
public string? Filter { get; set; }  // 按内容过滤

[JsonPropertyName("dryRun")]
public bool DryRun { get; set; }  // 预览模式
```

### Step 2: 更新 JSON 序列化上下文

在 [AstCliJsonContext.cs](file:///g:/Project/AI相关/McpHost/src/Plugins/AstCli/Models/AstCliJsonContext.cs) 中添加：

```csharp
[JsonSerializable(typeof(StringLiteralInfoDto))]
[JsonSerializable(typeof(StringQueryResultDto))]
[JsonSerializable(typeof(StringInsertResultDto))]
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
    private static readonly string[] s_excludedDirs = ["bin", "obj", ".git", "node_modules", ".vs"];
    private static readonly TimeSpan s_lockTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 查询项目中的字符串字面量
    /// </summary>
    public static async Task<StringQueryResultDto> QueryAsync(
        string projectPath, string? filePath, string? prefix, string? filter)

    /// <summary>
    /// 核心变换：在字符串的指定位置插入文本
    /// </summary>
    public static async Task<StringInsertResultDto> InsertAsync(
        string projectPath, string? filePath, string insertText,
        int position, string mode, string? filter, bool dryRun)
}

// CSharpSyntaxWalker 用于收集字符串信息
file sealed class StringLiteralCollector : CSharpSyntaxWalker
{
    // VisitLiteralExpression — 收集常规/逐字/原始字符串
    // VisitInterpolatedStringExpression — 收集插值字符串
}

// CSharpSyntaxRewriter 用于变换字符串
file sealed class StringLiteralRewriter : CSharpSyntaxRewriter
{
    private readonly string _insertText;
    private readonly int _position;      // 插入位置
    private readonly string? _filter;    // 过滤条件
    private int _transformedCount;

    // VisitLiteralExpression — 处理常规/逐字/原始字符串
    //   1. 获取 valueText
    //   2. 检查 filter
    //   3. 在 _position 处插入 _insertText
    //   4. 创建新 Token 替换

    // VisitInterpolatedStringExpression — 处理插值字符串
    //   1. 遍历 Contents 计算文本偏移
    //   2. 定位到 _position 对应的文本部分
    //   3. 拆分/修改文本部分
}
```

**字符串类型判断逻辑**：

```csharp
private static string GetStringKind(SyntaxToken token)
{
    var text = token.Text;
    if (text.StartsWith("\"\"\"", StringComparison.Ordinal))
        return "Raw";
    if (text.StartsWith("@\"", StringComparison.Ordinal))
        return "Verbatim";
    return "Regular";
}

private static string GetInterpolatedStringKind(InterpolatedStringExpressionSyntax node)
{
    var text = node.ToString();
    if (text.StartsWith("$\"\"\"", StringComparison.Ordinal) || text.StartsWith("$@\"\"\"", StringComparison.Ordinal))
        return "InterpolatedRaw";
    if (text.StartsWith("$@\"", StringComparison.Ordinal) || text.StartsWith("@$\"", StringComparison.Ordinal))
        return "VerbatimInterpolated";
    return "Interpolated";
}
```

**核心插入逻辑**（常规/逐字/原始字符串）：

```csharp
public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
{
    if (node.Kind() != SyntaxKind.StringLiteralExpression)
        return base.VisitLiteralExpression(node);

    var valueText = node.Token.ValueText;
    if (_filter != null && !valueText.Contains(_filter, StringComparison.OrdinalIgnoreCase))
        return base.VisitLiteralExpression(node);

    // 核心：在 _position 处插入 _insertText
    var position = Math.Clamp(_position, 0, valueText.Length);
    var newValue = string.Concat(valueText.AsSpan(0, position), _insertText, valueText.AsSpan(position));

    var kind = GetStringKind(node.Token);
    SyntaxToken newToken = kind switch
    {
        "Verbatim" => CreateVerbatimLiteral(newValue),
        "Raw" => CreateRawLiteral(node.Token, newValue),
        _ => SyntaxFactory.Literal(newValue)
    };

    _transformedCount++;
    return node.WithToken(newToken);
}
```

### Step 4: 添加 Schema 模板

在 [AstToolSchemaTemplates.cs](file:///g:/Project/AI相关/McpHost/src/Plugins/AstCli/Schemas/AstToolSchemaTemplates.cs) 中添加 4 个 Schema 方法：

```csharp
public static JsonElement StringQuerySchema() { ... }
public static JsonElement StringPrefixSchema() { ... }
public static JsonElement StringSuffixSchema() { ... }
public static JsonElement StringInsertSchema() { ... }
```

### Step 5: 添加命令处理

在 [CommandHandler.cs](file:///g:/Project/AI相关/McpHost/src/Plugins/AstCli/Commands/CommandHandler.cs) 中添加：

```csharp
"string_query" => await StringQueryAsync(request),
"string_prefix" => await StringPrefixAsync(request),
"string_suffix" => await StringSuffixAsync(request),
"string_insert" => await StringInsertAsync(request),
```

以及对应的 4 个处理方法。

更新 `ListTools()` 中的 `CommandCount = 13`（原 9 + 新增 4）。

更新 `ListCommands()` 添加 4 个新工具定义。

### Step 6: 单元测试

在 `tests/UnitTests/` 中添加 `StringLiteralEngineTests.cs`，覆盖：

1. **查询测试**：

   * 常规字符串识别

   * 逐字字符串识别

   * 原始字符串识别

   * 插值字符串识别

   * 前缀过滤

   * 内容过滤

   * 排除注释中的"字符串"

2. **变换测试**：

   * `string_prefix`：常规字符串写入前缀

   * `string_suffix`：常规字符串写入后缀

   * `string_insert`：常规字符串中间插入

   * 逐字字符串写入前缀

   * 原始字符串写入前缀

   * 插值字符串写入前缀

   * DryRun 模式不修改文件

   * filter 过滤功能

   * position 边界值（0、负数、超出长度）

### Step 7: 编译验证

```powershell
dotnet build McpHost.slnx -c Release
dotnet test McpHost.slnx -c Release
```

## 文件变更清单

| 文件                                                     | 操作 | 说明                             |
| ------------------------------------------------------ | -- | ------------------------------ |
| `src/Plugins/AstCli/Models/AstCliModels.cs`            | 修改 | 添加 3 个 DTO + AstCliRequest 新字段 |
| `src/Plugins/AstCli/Models/AstCliJsonContext.cs`       | 修改 | 添加 5 个 `[JsonSerializable]`    |
| `src/Plugins/AstCli/Services/StringLiteralEngine.cs`   | 新建 | 字符串查询与插入引擎                     |
| `src/Plugins/AstCli/Schemas/AstToolSchemaTemplates.cs` | 修改 | 添加 4 个 Schema 方法               |
| `src/Plugins/AstCli/Commands/CommandHandler.cs`        | 修改 | 添加 4 个命令处理 + 更新工具列表            |
| `tests/UnitTests/StringLiteralEngineTests.cs`          | 新建 | 单元测试                           |

## 自主决策记录

* **决策**: 将功能放在 AstCli 插件中而非新建项目

* **原因**: AstCli 已有 Roslyn 基础设施和 CSharpSyntaxRewriter 使用先例，避免重复引入依赖

* **替代方案**: 新建独立 CLI 工具（增加维护成本，且 Roslyn 依赖较重不宜放入 Common）

* **决策**: 三个变换命令共享一个核心操作（在位置 N 插入文本）

* **原因**: prefix/suffix/insert 本质都是字符串插入操作，统一核心减少重复代码

* **决策**: 使用 CSharpSyntaxWalker 收集信息 + CSharpSyntaxRewriter 变换代码

* **原因**: 遵循 AstEngine 的既有模式，Walker 只读遍历，Rewriter 可变换节点

