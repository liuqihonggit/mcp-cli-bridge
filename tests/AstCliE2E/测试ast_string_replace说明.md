# 测试 ast_string_replace 说明

> 本文档指导如何安全地测试 `ast_string_replace` 功能，避免直接修改主工程代码。

## 核心思路

使用 `publish\CodeDemo` 作为隔离测试环境，复制代码进去 → 用 AstCli.exe 执行替换 → 编译验证 → 如果有错误则修改本工程代码。

## 前置条件

- 已执行 `.\build.ps1` 发布到 `publish\` 目录
- AstCli.exe 位于 `publish\Plugins\AstCli\AstCli.exe`

## 步骤

### 1. 准备 CodeDemo 环境

删除并重新创建 `publish\CodeDemo`，复制 `src` 和 `tests`（排除 bin/obj）：

```powershell
# 删除旧的 CodeDemo（如果存在）
if (Test-Path "publish\CodeDemo") { Remove-Item "publish\CodeDemo" -Recurse -Force }

# 创建目录
New-Item -ItemType Directory -Path "publish\CodeDemo" -Force

# 复制 src 和 tests，排除 bin/obj
robocopy "src" "publish\CodeDemo\src" /E /XD bin obj /NFL /NDL /NJH /NJS /NC /NS
robocopy "tests" "publish\CodeDemo\tests" /E /XD bin obj /NFL /NDL /NJH /NJS /NC /NS
```

> robocopy 退出码 1 表示有文件被复制，是正常的。

### 2. 执行字符串替换

AstCli.exe 使用 Base64 编码的 JSON 作为输入。构造 JSON → Base64 编码 → 执行：

```powershell
# 构造请求 JSON（根据需要修改 pattern/replacement/filter）
$json = '{"command":"string_replace","projectPath":"G:\\Project\\AI相关\\McpHost\\publish\\CodeDemo","pattern":"memory_","replacement":"men_","filter":"memory_"}'

# Base64 编码
$b64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($json))

# 执行替换
publish\Plugins\AstCli\AstCli.exe --json-input $b64
```

#### 请求参数说明

| 字段 | 说明 | 示例 |
|------|------|------|
| `command` | 固定为 `string_replace` | `"string_replace"` |
| `projectPath` | CodeDemo 的绝对路径 | `"G:\\Project\\AI相关\\McpHost\\publish\\CodeDemo"` |
| `pattern` | 要查找的字符串或正则 | `"memory_"` |
| `replacement` | 替换文本 | `"men_"` |
| `filter` | 只修改包含此子串的字符串 | `"memory_"` |
| `useRegex` | 是否使用正则匹配（默认 false） | `true` |
| `dryRun` | 预览模式，不实际修改文件（默认 false） | `true` |

#### 正则替换示例

```powershell
$json = '{"command":"string_replace","projectPath":"G:\\Project\\AI相关\\McpHost\\publish\\CodeDemo","pattern":"memory_(\\w+)","replacement":"men_$1","useRegex":true,"filter":"memory_"}'
$b64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($json))
publish\Plugins\AstCli\AstCli.exe --json-input $b64
```

#### 预览模式（不修改文件）

```powershell
$json = '{"command":"string_replace","projectPath":"G:\\Project\\AI相关\\McpHost\\publish\\CodeDemo","pattern":"memory_","replacement":"men_","filter":"memory_","dryRun":true}'
$b64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($json))
publish\Plugins\AstCli\AstCli.exe --json-input $b64
```

### 3. 编译验证

依次编译 CodeDemo 中的项目，确保替换后代码仍然合法：

```powershell
# 编译主工程
dotnet build "publish\CodeDemo\src\McpHost\McpHost.csproj" -c Release

# 编译单元测试
dotnet build "publish\CodeDemo\tests\UnitTests\MyMemoryServer.UnitTests.csproj" -c Release

# 编译 E2E 测试
dotnet build "publish\CodeDemo\tests\E2E\MyMemoryServer.E2E.csproj" -c Release
```

### 4. 错误处理

**如果编译失败**：说明 `ast_string_replace` 工具存在 bug，需要修改本工程代码：

1. 在 `tests\UnitTests\StringLiteral\StringLiteralEngineTests.cs` 中编写 DTT 测试复现问题
2. 修复 `src\Plugins\AstCli\Services\StringLiteralEngine.cs` 中的 bug
3. 运行单元测试验证修复：`dotnet test tests/UnitTests --filter "StringLiteral" -v n`
4. 重新发布：`.\build.ps1`
5. 从步骤 1 重新开始

**如果编译成功**：可以在主工程上执行同样的替换操作。

## 其他 ast_string_* 命令

除 `string_replace` 外，还有以下字符串操作命令：

| 命令 | 说明 | 额外参数 |
|------|------|----------|
| `string_query` | 查询字符串字面量 | `prefix`, `filter` |
| `string_prefix` | 在字符串开头插入文本 | `insertText`, `filter`, `dryRun` |
| `string_suffix` | 在字符串末尾插入文本 | `insertText`, `filter`, `dryRun` |
| `string_insert` | 在指定位置插入文本 | `insertText`, `position`, `filter`, `dryRun` |
| `string_replace` | 替换字符串内容 | `pattern`, `replacement`, `useRegex`, `filter`, `dryRun` |

## 已知修复记录

### Bug 1：原始字符串字面量替换后内容重复

- **现象**：`"""memory_test"""` 替换后变成 `"""memory_test"""men_test"""memory_test"""`
- **原因**：`CreateRawLiteral` 用 `IndexOf('"')` 和 `LastIndexOf('"')` 提取定界符，把整个字符串（包括内容）都当成了定界符
- **修复**：改为从开头数引号数量来提取定界符

### Bug 2：多行原始字符串字面量替换后缩进丢失

- **现象**：多行 `"""..."""` 替换后闭合 `"""` 不在行首，导致 CS8999 错误
- **原因**：`CreateRawLiteral` 重建时丢失了换行和缩进信息
- **修复**：检测多行原始字符串，提取闭合行前的缩进，给新值的每一行添加缩进前缀
