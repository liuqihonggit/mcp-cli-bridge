# AstCli - AST 代码分析与重构工具

## 概述

AstCli 提供 C# 代码的 AST（抽象语法树）分析与重构能力，支持符号查询、引用查找、重命名替换、项目概览、文件上下文、编译诊断、符号大纲、字符串操作和**异步迁移**。

## 命令列表

### ast_symbol_query

在 C# 项目中按名称查询符号。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `symbol_query` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 符号名称（支持 `*` 通配符） |
| scope | string | 否 | 搜索范围：`project`（默认）或 `file`（仅顶层目录） |

### ast_reference_find

查找符号在项目中的所有引用。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `reference_find` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 要查找引用的符号名称 |

### ast_symbol_rename

跨文件重命名符号。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `symbol_rename` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 当前符号名称 |
| newName | string | 是 | 新符号名称 |

### ast_symbol_replace

跨文件替换符号名称。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `symbol_replace` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 旧符号名称 |
| newName | string | 是 | 新符号名称 |

### ast_symbol_info

获取文件指定位置的符号信息。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `symbol_info` |
| filePath | string | 是 | 源文件路径 |
| lineNumber | integer | 是 | 行号（0 起始） |
| columnNumber | integer | 是 | 列号（0 起始） |

### ast_workspace_overview

获取项目结构概览：文件统计、命名空间树、csproj 引用、目录角色推断、入口点识别。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `workspace_overview` |
| projectPath | string | 是 | 项目根目录路径 |

### ast_file_context

分析文件上下文：using 语句、同项目符号引用、同命名空间公开符号、反向依赖。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `file_context` |
| projectPath | string | 是 | 项目根目录路径 |
| filePath | string | 是 | 目标文件路径 |

### ast_diagnostics

获取语法诊断信息（仅语法层，不含语义错误）。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `diagnostics` |
| projectPath | string | 是 | 项目根目录路径 |
| filePath | string | 否 | 指定文件（不传则扫描整个项目） |

### ast_symbol_outline

获取文件符号大纲：类型声明、成员列表、行号范围、访问修饰符。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `symbol_outline` |
| filePath | string | 是 | 目标文件路径 |

## 异步迁移命令

将同步代码迁移为异步代码的 5 个单步 AST 命令。每个命令执行一步原子操作，LLM 可按需组合调用。

### 迁移工作流

将 `SendLog("msg")` 迁移为 `await SendLogAsync("msg").ConfigureAwait(false)` 的推荐步骤：

```
步骤1: async_rename      → SendLog 重命名为 SendLogAsync（同时改声明和调用点）
步骤2: async_return_type  → void 改为 Task, T 改为 Task<T>
步骤3: async_add_modifier → 方法声明加 async 关键字
步骤4: async_add_await    → 调用点加 await（可选加 .ConfigureAwait(false)）
步骤5: async_param_add    → 方法声明追加参数（如 CancellationToken ct）
```

**关键原则**：每步独立执行，可单独 dryRun 预览，可跳过不需要的步骤。

### ast_async_rename

重命名方法及其所有调用点（声明 + 引用同步修改）。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `async_rename` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 当前方法名（如 `SendLog`） |
| newName | string | 是 | 新方法名（如 `SendLogAsync`） |
| filePath | string | 否 | 指定文件（不传则修改整个项目） |
| dryRun | boolean | 否 | 预览模式：不实际修改文件（默认 false） |

**示例**：`SendLog` → `SendLogAsync`，同时修改声明和所有 `SendLog(...)` 调用点。

### ast_async_add_modifier

给方法声明添加 `async` 修饰符。已含 `async` 的方法不会重复添加。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `async_add_modifier` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 方法名 |
| filePath | string | 否 | 指定文件（不传则修改整个项目） |
| dryRun | boolean | 否 | 预览模式（默认 false） |

### ast_async_return_type

修改方法返回类型：`void` → `Task`，`T` → `Task<T>`。已经是 `Task`/`Task<T>` 的不会重复包装。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `async_return_type` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 方法名 |
| filePath | string | 否 | 指定文件（不传则修改整个项目） |
| dryRun | boolean | 否 | 预览模式（默认 false） |

**转换规则**：
- `void` → `Task`
- `string` → `Task<string>`
- `int` → `Task<int>`
- `Task` → 不变
- `Task<T>` → 不变

### ast_async_add_await

给方法调用点添加 `await`，可选追加 `.ConfigureAwait(false)`。已 await 的调用不会重复添加。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `async_add_await` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 要 await 的方法名 |
| addConfigureAwait | boolean | 否 | 是否追加 `.ConfigureAwait(false)`（默认 false） |
| filePath | string | 否 | 指定文件（不传则修改整个项目） |
| dryRun | boolean | 否 | 预览模式（默认 false） |

**转换示例**：
- `SendLogAsync("msg")` → `await SendLogAsync("msg")`
- `var r = GetNameAsync()` → `var r = await GetNameAsync()`
- 加 ConfigureAwait：`await SendLogAsync("msg").ConfigureAwait(false)`

### ast_async_param_add

给方法声明追加参数。已存在同名参数不会重复添加。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `async_param_add` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 方法名 |
| paramType | string | 是 | 参数类型（如 `CancellationToken`） |
| paramName | string | 是 | 参数名（如 `ct`） |
| filePath | string | 否 | 指定文件（不传则修改整个项目） |
| dryRun | boolean | 否 | 预览模式（默认 false） |

**转换示例**：
- `Task SendLogAsync(string msg)` → `Task SendLogAsync(string msg, CancellationToken ct)`

## 同步迁移命令（逆向操作）

将异步代码还原为同步代码的 4 个单步 AST 命令。与异步迁移命令互为逆操作。

### 逆向迁移工作流

将 `await SendLogAsync("msg").ConfigureAwait(false)` 还原为 `SendLog("msg")` 的推荐步骤：

```
步骤1: sync_remove_await    → 移除 await 和 .ConfigureAwait(false)
步骤2: sync_remove_modifier → 移除 async 关键字
步骤3: sync_return_type     → Task→void, Task<T>→T
步骤4: async_rename         → SendLogAsync 重命名为 SendLog（复用已有命令）
步骤5: sync_param_remove    → 移除 CancellationToken ct 等参数
```

**注意**：`async_rename` 命令本身是双向的，直接传 `symbolName=SendLogAsync` + `newName=SendLog` 即可实现逆向重命名。

### ast_sync_remove_modifier

从方法声明移除 `async` 修饰符。不含 `async` 的方法不会改变。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `sync_remove_modifier` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 方法名 |
| filePath | string | 否 | 指定文件（不传则修改整个项目） |
| dryRun | boolean | 否 | 预览模式（默认 false） |

**转换示例**：
- `public async Task SendLog(string msg)` → `public Task SendLog(string msg)`

### ast_sync_return_type

解包方法返回类型：`Task` → `void`，`Task<T>` → `T`。非 Task 类型不会改变。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `sync_return_type` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 方法名 |
| filePath | string | 否 | 指定文件（不传则修改整个项目） |
| dryRun | boolean | 否 | 预览模式（默认 false） |

**转换规则**：
- `Task` → `void`
- `Task<string>` → `string`
- `Task<int>` → `int`
- `void` → 不变
- `string` → 不变

### ast_sync_remove_await

移除方法调用点的 `await`，同时移除 `.ConfigureAwait(false)`。非目标方法的 await 不会改变。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `sync_remove_await` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 要移除 await 的方法名 |
| filePath | string | 否 | 指定文件（不传则修改整个项目） |
| dryRun | boolean | 否 | 预览模式（默认 false） |

**转换示例**：
- `await SendLogAsync("msg")` → `SendLogAsync("msg")`
- `await SendLogAsync("msg").ConfigureAwait(false)` → `SendLogAsync("msg")`
- `var r = await GetNameAsync()` → `var r = GetNameAsync()`
- `var r = await GetNameAsync().ConfigureAwait(false)` → `var r = GetNameAsync()`

### ast_sync_param_remove

从方法声明移除指定参数。参数不存在时不会改变。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `sync_param_remove` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 方法名 |
| paramName | string | 是 | 要移除的参数名（如 `ct`） |
| filePath | string | 否 | 指定文件（不传则修改整个项目） |
| dryRun | boolean | 否 | 预览模式（默认 false） |

**转换示例**：
- `Task SendLogAsync(string msg, CancellationToken ct)` → `Task SendLogAsync(string msg)`
- `Task SendLogAsync(CancellationToken ct)` → `Task SendLogAsync()`

## 特性

- **无状态**：每次调用即时解析，不缓存 AST
- **AOT 兼容**：支持 NativeAOT 编译
- **文件锁安全**：使用 FileLockService 确保并发安全
- **自动排除**：跳过 bin/obj/.git/node_modules 目录
- **dryRun 预览**：所有修改命令支持 dryRun 模式，先预览再执行
- **双向迁移**：5 个异步迁移 + 4 个同步迁移 + async_rename 双向复用 = 10 个迁移命令
- **18 个命令**：5 个符号操作 + 1 个引用操作 + 1 个工作区 + 1 个文件上下文 + 1 个诊断 + 5 个异步迁移 + 4 个同步迁移
