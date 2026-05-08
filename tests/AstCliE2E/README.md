# AstCli String Operations E2E Test

> 独立的端到端测试项目，验证 AstCli 所有字符串操作命令的正确性。
> 不加入 CI/CD，手动运行。

## 运行

```powershell
# 前置：先发布
.\build.ps1

# 运行 E2E 测试
dotnet run --project tests/AstCliE2E/AstCli.E2E.csproj -c Release
```

退出码 = 失败数（0 表示全部通过）。

## 工作原理

每组测试的完整流程：

```
1. 清理并复制 CodeDemo
   ├── 删除 publish/CodeDemo/
   ├── 复制 src/（排除 bin/obj）
   ├── 复制 tests/（排除 bin/obj/AstCliE2E）
   ├── 复制 McpHost.slnx、nuget.config、Directory.Build.props
   └── 复制 lib/（排除 bin/obj）

2. 执行 AstCli 字符串操作
   └── 通过 AstCli.exe --json-input <Base64JSON> 调用

3. 编译验证
   ├── Common.Contracts
   ├── Common
   ├── McpHost
   ├── MemoryCli
   ├── FileReaderCli
   ├── AstCli
   ├── UnitTests
   └── E2E

4. 单元测试验证
   ├── dotnet test（全部单元测试可发现并可执行）
   └── dotnet test --filter "StringLiteral"（字符串相关测试可执行）
```

> **注意**：字符串操作会替换测试代码中的字面量，导致部分测试断言失败。
> 因此只验证测试可发现和可执行，不要求全部通过。

## 测试覆盖

| 命令 | 测试 | 验证内容 |
|------|------|----------|
| `string_query` | 3 | filter 过滤、prefix 过滤、指定文件查询 |
| `string_replace` | 4 | 字面量替换 + 编译 + 单元测试可执行 |
| `string_prefix` | 3 | 前缀插入 + 编译 + 单元测试可执行 |
| `string_suffix` | 3 | 后缀插入 + 编译 + 单元测试可执行 |
| `string_insert` | 3 | 位置插入 + 编译 + 单元测试可执行 |
| `dryRun` | 1 | 预览模式不修改文件 |
| `regex replace` | 4 | 正则替换 + 编译 + 单元测试可执行 |

**共 21 个测试用例**

## 文件说明

| 文件 | 职责 |
|------|------|
| `AstCliRunner.cs` | 封装 AstCli.exe 调用（JSON→Base64→进程→解析 `data` 属性） |
| `CodeDemoHelper.cs` | CodeDemo 环境管理（清理/复制/编译/单元测试/字符串计数） |
| `Program.cs` | 21 个测试用例入口 |

## 修复 bug 后的验证流程

1. 修改 `src/Plugins/AstCli/` 中的代码
2. 运行单元测试：`dotnet test tests/UnitTests --filter "StringLiteral" -v n`
3. 发布：`.\build.ps1`
4. 运行本 E2E 测试：`dotnet run --project tests/AstCliE2E/AstCli.E2E.csproj -c Release`
5. 全部通过后再在主项目上执行实际操作

## 已知修复记录

### Bug 1：原始字符串字面量替换后内容重复

- **现象**：`"""memory_test"""` 替换后变成 `"""memory_test"""men_test"""memory_test"""`
- **原因**：`CreateRawLiteral` 用 `IndexOf('"')` 和 `LastIndexOf('"')` 提取定界符，把整个字符串（包括内容）都当成了定界符
- **修复**：改为从开头数引号数量来提取定界符
- **发现方式**：单元测试 `Replace_Literal_RawString_ShouldNotDuplicateContent`

### Bug 2：多行原始字符串字面量替换后缩进丢失

- **现象**：多行 `"""..."""` 替换后闭合 `"""` 不在行首，导致 CS8999 错误
- **原因**：`CreateRawLiteral` 重建时丢失了换行和缩进信息
- **修复**：检测多行原始字符串，提取闭合行前的缩进，给新值的每一行添加缩进前缀
- **发现方式**：单元测试 `Replace_Literal_MultiLineRawString_ShouldPreserveIndentation`
