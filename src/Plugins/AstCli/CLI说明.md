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

## 异步/同步迁移命令

9 个单步 AST 命令（5 个异步迁移 + 4 个同步迁移），加上双向复用的 `async_rename`，共 10 个迁移能力。每个命令执行一步原子操作。

### 命令速查

| 命令 | 方向 | 操作 |
|------|------|------|
| `async_rename` | 双向 | 重命名方法 + 所有调用点 |
| `async_return_type` | 同步→异步 | void→Task, T→Task\<T\> |
| `async_add_modifier` | 同步→异步 | 加 async 关键字 |
| `async_add_await` | 同步→异步 | 加 await + 可选 .ConfigureAwait(false) |
| `async_param_add` | 同步→异步 | 追加参数 |
| `sync_return_type` | 异步→同步 | Task→void, Task\<T\>→T |
| `sync_remove_modifier` | 异步→同步 | 移除 async 关键字 |
| `sync_remove_await` | 异步→同步 | 移除 await + .ConfigureAwait(false) |
| `sync_param_remove` | 异步→同步 | 移除参数 |

### 案例1：将同步方法迁移为异步（含 ConfigureAwait）

<thinking>
用户要求将 SendLog 方法从同步改为异步。我需要分析当前代码状态，然后按正确顺序执行迁移步骤。

当前代码：
```csharp
public class Logger
{
    public void SendLog(string msg)
    {
        Console.WriteLine(msg);
    }

    public void Process()
    {
        SendLog("start");
        SendLog("end");
    }
}
```

迁移目标：`void SendLog(string msg)` → `async Task SendLogAsync(string msg, CancellationToken ct)`，调用点加 `await ... .ConfigureAwait(false)`。

步骤分析：
1. 先重命名：SendLog → SendLogAsync（声明和调用点一起改，避免编译错误）
2. 改返回类型：void → Task（此时方法签名变为 Task SendLogAsync）
3. 加 async 修饰符（此时方法签名变为 async Task SendLogAsync）
4. 调用点加 await + ConfigureAwait（因为这是库代码，需要 ConfigureAwait(false)）
5. 追加 CancellationToken 参数

每步执行前应先用 dryRun=true 预览，确认无误后再实际执行。
</thinking>

**步骤1：重命名方法**
```
tool_execute: ast_async_rename
{
  "command": "async_rename",
  "projectPath": "G:\\MyProject",
  "symbolName": "SendLog",
  "newName": "SendLogAsync",
  "dryRun": true
}
```
预览确认后，`dryRun: false` 正式执行。

**步骤2：修改返回类型**
```
tool_execute: ast_async_return_type
{
  "command": "async_return_type",
  "projectPath": "G:\\MyProject",
  "symbolName": "SendLogAsync",
  "dryRun": true
}
```
void → Task

**步骤3：添加 async 修饰符**
```
tool_execute: ast_async_add_modifier
{
  "command": "async_add_modifier",
  "projectPath": "G:\\MyProject",
  "symbolName": "SendLogAsync",
  "dryRun": true
}
```

**步骤4：调用点加 await + ConfigureAwait**
```
tool_execute: ast_async_add_await
{
  "command": "async_add_await",
  "projectPath": "G:\\MyProject",
  "symbolName": "SendLogAsync",
  "addConfigureAwait": true,
  "dryRun": true
}
```
`SendLogAsync("start")` → `await SendLogAsync("start").ConfigureAwait(false)`

**步骤5：追加 CancellationToken 参数**
```
tool_execute: ast_async_param_add
{
  "command": "async_param_add",
  "projectPath": "G:\\MyProject",
  "symbolName": "SendLogAsync",
  "paramType": "CancellationToken",
  "paramName": "ct",
  "dryRun": true
}
```

**最终结果**：
```csharp
public class Logger
{
    public async Task SendLogAsync(string msg, CancellationToken ct)
    {
        Console.WriteLine(msg);
    }

    public async Task Process()
    {
        await SendLogAsync("start").ConfigureAwait(false);
        await SendLogAsync("end").ConfigureAwait(false);
    }
}
```

⚠️ **注意**：Process 方法也需要手动迁移（async_return_type + async_add_modifier），因为 `async_add_await` 只改调用点，不会自动改调用方的方法签名。

### 案例2：将有返回值的方法迁移为异步

<thinking>
用户要求将 GetName 方法从同步改为异步。这个方法有返回值 string，需要变为 Task<string>。

当前代码：
```csharp
public class UserService
{
    public string GetName() { return "Alice"; }

    public void Greet()
    {
        var name = GetName();
        Console.WriteLine($"Hello {name}");
    }
}
```

步骤与案例1类似，但 async_return_type 会自动处理 string → Task<string> 的包装。
async_add_await 处理赋值语句时：var name = GetNameAsync() → var name = await GetNameAsync()
</thinking>

**步骤1-3**：同案例1（rename → return_type → add_modifier）

**步骤4：调用点加 await**
```
tool_execute: ast_async_add_await
{
  "command": "async_add_await",
  "projectPath": "G:\\MyProject",
  "symbolName": "GetNameAsync",
  "addConfigureAwait": true,
  "dryRun": true
}
```
`var name = GetNameAsync()` → `var name = await GetNameAsync().ConfigureAwait(false)`

**最终结果**：
```csharp
public class UserService
{
    public async Task<string> GetNameAsync() { return "Alice"; }

    public async Task Greet()
    {
        var name = await GetNameAsync().ConfigureAwait(false);
        Console.WriteLine($"Hello {name}");
    }
}
```

### 案例3：逆向迁移——将异步方法还原为同步

<thinking>
用户要求将异步的 SendLogAsync 还原为同步的 SendLog。这是案例1的逆向操作，步骤顺序与正向相反。

当前代码：
```csharp
public class Logger
{
    public async Task SendLogAsync(string msg, CancellationToken ct)
    {
        Console.WriteLine(msg);
    }

    public async Task Process()
    {
        await SendLogAsync("start").ConfigureAwait(false);
        await SendLogAsync("end").ConfigureAwait(false);
    }
}
```

逆向步骤分析：
1. 先移除调用点的 await + ConfigureAwait（如果先改返回类型，调用点 await void 会编译错误）
2. 移除 async 修饰符
3. 改返回类型 Task→void
4. 重命名 SendLogAsync → SendLog（async_rename 双向复用）
5. 移除 CancellationToken 参数

顺序关键：必须先移除 await，再改返回类型，否则 await 一个 void 方法会编译错误。
</thinking>

**步骤1：移除 await + ConfigureAwait**
```
tool_execute: ast_sync_remove_await
{
  "command": "sync_remove_await",
  "projectPath": "G:\\MyProject",
  "symbolName": "SendLogAsync",
  "dryRun": true
}
```
`await SendLogAsync("start").ConfigureAwait(false)` → `SendLogAsync("start")`

**步骤2：移除 async 修饰符**
```
tool_execute: ast_sync_remove_modifier
{
  "command": "sync_remove_modifier",
  "projectPath": "G:\\MyProject",
  "symbolName": "SendLogAsync",
  "dryRun": true
}
```

**步骤3：改返回类型 Task→void**
```
tool_execute: ast_sync_return_type
{
  "command": "sync_return_type",
  "projectPath": "G:\\MyProject",
  "symbolName": "SendLogAsync",
  "dryRun": true
}
```

**步骤4：重命名 SendLogAsync → SendLog**
```
tool_execute: ast_async_rename
{
  "command": "async_rename",
  "projectPath": "G:\\MyProject",
  "symbolName": "SendLogAsync",
  "newName": "SendLog",
  "dryRun": true
}
```
注意：`async_rename` 是双向命令，正向逆向都可以用。

**步骤5：移除 CancellationToken 参数**
```
tool_execute: ast_sync_param_remove
{
  "command": "sync_param_remove",
  "projectPath": "G:\\MyProject",
  "symbolName": "SendLog",
  "paramName": "ct",
  "dryRun": true
}
```

**最终结果**：
```csharp
public class Logger
{
    public void SendLog(string msg)
    {
        Console.WriteLine(msg);
    }

    public void Process()
    {
        SendLog("start");
        SendLog("end");
    }
}
```

### 案例4：部分迁移——只改方法签名不改调用方

<thinking>
有时用户只想改底层方法为异步，调用方暂时保持同步（fire-and-forget 模式）。

当前代码：
```csharp
public class Cache
{
    public void Refresh() { /* IO操作 */ }

    public void OnDataChanged()
    {
        Refresh();
    }
}
```

用户意图：Refresh 改为异步，但 OnDataChanged 暂时不改（调用方后续再处理）。
此时不需要执行 async_add_await，让调用方以 fire-and-forget 方式调用。
</thinking>

**步骤1-3**：rename → return_type → add_modifier（同案例1）

**不执行步骤4**：跳过 `async_add_await`，调用方保持 `RefreshAsync()` 同步调用（fire-and-forget）

**步骤5**：追加 CancellationToken
```
tool_execute: ast_async_param_add
{
  "command": "async_param_add",
  "projectPath": "G:\\MyProject",
  "symbolName": "RefreshAsync",
  "paramType": "CancellationToken",
  "paramName": "ct",
  "dryRun": true
}
```

**最终结果**：
```csharp
public class Cache
{
    public async Task RefreshAsync(CancellationToken ct) { /* IO操作 */ }

    public void OnDataChanged()
    {
        RefreshAsync();  // fire-and-forget，后续再迁移此调用方
    }
}
```

### 案例5：只移除 ConfigureAwait（保留 await）

<thinking>
用户要求移除 .ConfigureAwait(false) 但保留 await。这种场景没有单独命令，但可以通过组合实现：
1. 先 sync_remove_await 移除 await + ConfigureAwait
2. 再 async_add_await 只加 await（addConfigureAwait=false）

当前代码：
```csharp
await SendLogAsync("msg").ConfigureAwait(false);
```

目标：
```csharp
await SendLogAsync("msg");
```
</thinking>

**步骤1：移除 await + ConfigureAwait**
```
tool_execute: ast_sync_remove_await
{
  "command": "sync_remove_await",
  "projectPath": "G:\\MyProject",
  "symbolName": "SendLogAsync",
  "dryRun": true
}
```
结果：`SendLogAsync("msg")`

**步骤2：只加 await（不加 ConfigureAwait）**
```
tool_execute: ast_async_add_await
{
  "command": "async_add_await",
  "projectPath": "G:\\MyProject",
  "symbolName": "SendLogAsync",
  "addConfigureAwait": false,
  "dryRun": true
}
```
结果：`await SendLogAsync("msg")`

### 命令详细参数

#### ast_async_rename

重命名方法及其所有调用点（声明 + 引用同步修改）。**双向命令**：正向 `SendLog→SendLogAsync`，逆向 `SendLogAsync→SendLog`。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `async_rename` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 当前方法名 |
| newName | string | 是 | 新方法名 |
| filePath | string | 否 | 指定文件（不传则修改整个项目） |
| dryRun | boolean | 否 | 预览模式（默认 false） |

#### ast_async_return_type

修改方法返回类型：`void` → `Task`，`T` → `Task<T>`。已经是 `Task`/`Task<T>` 的不会重复包装。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `async_return_type` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 方法名 |
| filePath | string | 否 | 指定文件（不传则修改整个项目） |
| dryRun | boolean | 否 | 预览模式（默认 false） |

**转换规则**：`void`→`Task`，`string`→`Task<string>`，`int`→`Task<int>`，`Task`→不变，`Task<T>`→不变

#### ast_async_add_modifier

给方法声明添加 `async` 修饰符。已含 `async` 的方法不会重复添加。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `async_add_modifier` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 方法名 |
| filePath | string | 否 | 指定文件（不传则修改整个项目） |
| dryRun | boolean | 否 | 预览模式（默认 false） |

#### ast_async_add_await

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

#### ast_async_param_add

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

#### ast_sync_return_type

解包方法返回类型：`Task` → `void`，`Task<T>` → `T`。非 Task 类型不会改变。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `sync_return_type` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 方法名 |
| filePath | string | 否 | 指定文件（不传则修改整个项目） |
| dryRun | boolean | 否 | 预览模式（默认 false） |

**转换规则**：`Task`→`void`，`Task<string>`→`string`，`Task<int>`→`int`，`void`→不变，其他类型→不变

#### ast_sync_remove_modifier

从方法声明移除 `async` 修饰符。不含 `async` 的方法不会改变。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `sync_remove_modifier` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 方法名 |
| filePath | string | 否 | 指定文件（不传则修改整个项目） |
| dryRun | boolean | 否 | 预览模式（默认 false） |

#### ast_sync_remove_await

移除方法调用点的 `await`，同时移除 `.ConfigureAwait(false)`。非目标方法的 await 不会改变。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `sync_remove_await` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 要移除 await 的方法名 |
| filePath | string | 否 | 指定文件（不传则修改整个项目） |
| dryRun | boolean | 否 | 预览模式（默认 false） |

#### ast_sync_param_remove

从方法声明移除指定参数。参数不存在时不会改变。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `sync_param_remove` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 方法名 |
| paramName | string | 是 | 要移除的参数名 |
| filePath | string | 否 | 指定文件（不传则修改整个项目） |
| dryRun | boolean | 否 | 预览模式（默认 false） |

## 特性

- **无状态**：每次调用即时解析，不缓存 AST
- **AOT 兼容**：支持 NativeAOT 编译
- **文件锁安全**：使用 FileLockService 确保并发安全
- **自动排除**：跳过 bin/obj/.git/node_modules 目录
- **dryRun 预览**：所有修改命令支持 dryRun 模式，先预览再执行
- **双向迁移**：5 个异步迁移 + 4 个同步迁移 + async_rename 双向复用 = 10 个迁移命令
- **18 个命令**：5 个符号操作 + 1 个引用操作 + 1 个工作区 + 1 个文件上下文 + 1 个诊断 + 5 个异步迁移 + 4 个同步迁移
