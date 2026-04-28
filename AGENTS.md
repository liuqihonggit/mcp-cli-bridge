# McpHost 项目说明

## 项目概述

**项目名称**: @jingjingbox/mcp-cli-bridge\
**版本**: 3.0.3\
**定位**: MCP-CLI 桥接服务器 - 通过 MCP 协议调用 CLI 工具\
**目标平台**: Windows x64\
**技术栈**: .NET 10.0, NativeAOT, C# 13

### 核心价值

为 TraeCN 提供 MCP 接口支持，通过少量 MCP 工具查找和管理 CLI.exe 工具，降低上下文成本。

1, 这个项目是为了TraeCN而创建的.
它只支持MCP接口,在McpHost中通过少量的MCP来查找CLI.exe工具.
每个CLI.exe内部有多个工具,它们内部就不是MCP了,而是通过说明文档来调用,降低上下文成本.

每个CLI.exe的工具都不要缓存,
未来一个CLI内部可能有上万个命令,上万个描述.
工具内部可以有帮助,可以有`CLI说明.md`文档,
LLM可以动态获取,渐进式获取,但不能直接一股脑塞到缓存,一下子全部暴露给上下文. 

2, 通过 build.ps1 脚本来发布插件,插件会被发布到 publish/ 文件夹下面.
包括 npm包相关内容,以及 readme.md 文件.
不需要 配置文件,因为查找是同目录下面的CLI.exe文件.
发布时候,不能手动复制文件,只能去修改对应的生成逻辑.

3, src/Plugins/ 文件夹下面的都是外部插件,
不能在 src/ 等等内部项目中出现任何提及,只能通过协议进行获取调用函数的内容.

- MCP Server 应该只暴露 Host 层面的管理工具 （tool_search, tool_execute, tool_list 等）
- CLI 内部的工具 （memory_create_entities, file_reader_read_head） 不应该直接暴露
- 而是通过 tool_execute 来间接调用

***

## 架构设计

### 项目结构

```
McpHost/
├── src/
│   ├── McpHost/                    # 主机服务 (EXE)
│   ├── Common/                     # 共享基建 (DLL)
│   ├── Common.Contracts/           # 契约层 (DLL)
│   └── Plugins/                   # CLI 插件目录
│       ├── MemoryCli/              # 知识图谱 CLI (EXE)
│       └── FileReaderCli/          # 文件读取 CLI (EXE)
├── lib/
│   └── McpProtocol/                # 独立 NuGet 包
│       ├── McpProtocol/            # MCP 协议实现
│       └── McpProtocol.Contracts/  # MCP 协议契约
├── tests/
│   ├── UnitTests/                  # 单元测试
│   ├── SecurityTests/              # 安全测试
│   ├── E2E/                        # 端到端测试
│   └── Benchmarks/                 # 性能基准测试
└── publish/                        # 发布输出目录
```

### 核心组件

#### 1. McpHost (主机服务)

- **输出**: EXE
- **职责**:
  - MCP 协议服务器
  - CLI 插件管理
  - 进程池管理
  - 中间件管道
- **引用**: Common, Common.Contracts, McpProtocol

#### 2. Common (共享基建)

- **输出**: DLL
- **模块**:
  - Caching (缓存系统)
  - CliProtocol (CLI协议)
  - Configuration (配置管理)
  - Constants (常量管理)
  - IoC (依赖注入)
  - Json (JSON服务)
  - Logging (日志系统)
  - Middleware (中间件)
  - Plugins (插件基础设施)
  - Reflection (反射工具)
  - Security (安全系统)
  - Tools (工具基础设施)

#### 3. Common.Contracts (契约层)

- **输出**: DLL
- **原则**: 只能有接口/抽象，不能有具体实现
- **内容**:
  - 接口定义
  - 纯DTO模型
  - 枚举类型

#### 4. Plugins (CLI插件)

- **输出**: EXE
- **隔离原则**: 不能直接引用 McpHost
- **通信**: 通过 CLI 协议 (JSON-RPC 2.0)
- **现有插件**:
  - **MemoryCli**: 知识图谱管理 (7个命令)
  - **FileReaderCli**: 文件读取 (2个命令)

#### 5. McpProtocol (独立NuGet包)

- **输出**: DLL
- **定位**: 可被其他项目引用的独立包
- **内容**: MCP 协议解析和实现

***

## 技术规范

### 1. AOT 编译要求 ⚠️

**强制要求**: 所有项目必须支持 NativeAOT 编译

**原因**:

- npm 包发布需要小体积
- 非 AOT 会导致安装崩溃

**限制**:

- 禁止运行时反射 emit
- 禁止动态代码生成
- JSON 处理必须用类表示

### 2. JSON 处理规范

**❌ 禁止**:

```csharp
// 直接解析 JSON 字符串
var obj = JsonSerializer.Deserialize<dynamic>(json);
```

**✅ 正确**:

```csharp
// 使用类型化的类
public class MyData
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
}

var obj = JsonSerializer.Deserialize<MyData>(json, CommonJsonContext.Default.MyData);
```

### 3. 命名空间管理

**规则**: `.cs` 文件内禁止 `using` 语句

**位置**: 所有命名空间引用统一放在 `GlobalUsings.cs`

**冲突处理**: 使用简短别名在 GlobalUsings.cs 澄清

### 4. 参数封装原则

**❌ 避免**:

```csharp
public void Test(string cmd, string args, string options) { }
```

**✅ 正确**:

```csharp
public void Test(Command cmd) { }
```

**原因**: 解析不发生在总线

### 5. 硬编码替代方案

**❌ 禁止 if-else 链**:

```csharp
if (type == "A") return 1;
if (type == "B") return 2;
```

**✅ 使用字典**:

```csharp
private static readonly FrozenDictionary<string, int> TypeMap = 
    new Dictionary<string, int>
    {
        ["A"] = 1,
        ["B"] = 2
    }.ToFrozenDictionary();
```

### 6. 类型安全引用

**❌ 禁止**:

```csharp
var typeName = "MyClass";
```

**✅ 正确**:

```csharp
var typeName = nameof(MyClass);
var type = typeof(MyClass);
```

***

## CLI 插件系统

### 通信协议

**请求格式** (JSON-RPC 2.0):

```json
{
    "jsonrpc": "2.0",
    "method": "tool_name",
    "params": { ... },
    "id": 1
}
```

**响应格式**:

```json
{
    "jsonrpc": "2.0",
    "result": { ... },
    "id": 1
}
```

### 生命周期

1. McpHost 启动时扫描 `Plugins/` 目录
2. 通过 ProcessPool 管理 CLI 进程池
3. 每个进程处理多个请求后回收
4. 插件失败不影响主程序运行

### 安全隔离

- 独立进程隔离
- ProcessPool 限制资源
- 超时自动终止
- 崩溃不影响 McpHost

### 开发规范

**CLI 服务目录结构**:

```
MyCli/
├── MyCli.csproj
├── Program.cs
├── CLI说明.md
├── Commands/
│   └── CommandHandler.cs
├── Services/
│   └── MyService.cs
└── Validation/
    └── MyValidator.cs (可选)
```

**引用限制**:

- ✅ 可以引用: Common, Common.Contracts
- ❌ 禁止引用: McpHost

***

## MCP 工具暴露

### Host 层工具 (直接暴露)

- `tool_search` - 搜索工具
- `tool_execute` - 执行工具
- `tool_describe` - 描述工具
- `tool_list` - 列出工具
- `package_status` - 包状态
- `package_install` - 安装包

### CLI 层工具 (间接调用)

**MemoryCli 工具**:

- `memory_create_entities` - 创建实体
- `memory_create_relations` - 创建关系
- `memory_read_graph` - 读取图谱
- `memory_search_nodes` - 搜索节点
- `memory_add_observations` - 添加观察
- `memory_delete_entities` - 删除实体
- `memory_open_nodes` - 打开节点

**FileReaderCli 工具**:

- `file_reader_read_head` - 读取文件头
- `file_reader_read_tail` - 读取文件尾

**调用方式**: 通过 `tool_execute` 间接调用

***

## 测试策略

### 1. 单元测试

**位置**: `tests/UnitTests/`\
**框架**: xUnit\
**覆盖**: 153 个测试\
**运行**: `dotnet test McpHost.slnx`

### 2. 安全测试

**位置**: `tests/SecurityTests/`\
**覆盖**:

- 命令注入测试
- 输入验证测试
- 权限控制测试

### 3. E2E 测试

**位置**: `tests/E2E/`\
**目的**: 测试两个进程相互通讯\
**覆盖**: 23 个测试\
**运行**: `dotnet run --project tests\E2E\MyMemoryServer.E2E.csproj -c Release`

**测试内容**:

- MCP 协议测试
- MCP 层工具测试
- CLI 层工具测试
- ID 格式测试
- 错误处理测试

***

## 构建和发布

### 构建脚本

**文件**: `build.ps1`

**脚本职责**:
- 清理发布目录
- AOT 编译所有组件
- 复制 npm 包文件
- 验证输出完整性
- 显示构建结果

**执行步骤**:

```powershell
# 步骤 1: 清理 publish 目录
if (Test-Path "publish") {
    Get-ChildItem "publish\*" -Recurse -Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

# 步骤 2: 创建 publish 目录
New-Item -ItemType Directory -Path "publish" -Force | Out-Null

# 步骤 3: AOT 编译 McpHost
dotnet publish src\McpHost\McpHost.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishAot=true `
    -p:PublishDir="$PSScriptRoot\publish"

# 步骤 4: AOT 编译 MemoryCli
dotnet publish src\Plugins\MemoryCli\MemoryCli.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishAot=true `
    -p:PublishDir="$PSScriptRoot\publish"

# 步骤 5: AOT 编译 FileReaderCli
dotnet publish src\Plugins\FileReaderCli\FileReaderCli.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishAot=true `
    -p:PublishDir="$PSScriptRoot\publish"

# 步骤 6: 复制 npm 包文件
Copy-Item "package.json" "publish\" -Force
Copy-Item "index.js" "publish\" -Force
Copy-Item "README.md" "publish\" -Force

# 步骤 7: 复制 CLI 说明文档
Copy-Item "src\Plugins\MemoryCli\CLI说明.md" "publish\MemoryCli说明.md" -Force
Copy-Item "src\Plugins\FileReaderCli\CLI说明.md" "publish\FileReaderCli说明.md" -Force
```

**错误处理**:
- 每个编译步骤后检查 `$LASTEXITCODE`
- 失败时立即终止并输出错误信息
- 验证必需文件是否存在

**输出验证**:

```powershell
# 验证必需文件
$requiredFiles = @(
    "publish\McpHost.exe",
    "publish\MemoryCli.exe",
    "publish\FileReaderCli.exe"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path $file)) {
        Write-Error "Missing required file: $file"
        exit 1
    }
}
```

**运行方式**:

```powershell
.\build.ps1
```

### 发布输出

**目录**: `publish/`

**必需文件**:

| 文件 | 类型 | 说明 |
|------|------|------|
| McpHost.exe | EXE | MCP 主机服务 |
| MemoryCli.exe | EXE | 知识图谱 CLI 插件 |
| FileReaderCli.exe | EXE | 文件读取 CLI 插件 |
| index.js | JS | npm 入口文件 |
| package.json | JSON | npm 包配置 |
| README.md | MD | 包说明文档 |
| MemoryCli说明.md | MD | MemoryCli 使用说明 |
| FileReaderCli说明.md | MD | FileReaderCli 使用说明 |

**构建结果示例**:

```
Build completed successfully!

Published files:
  McpHost.exe (X,XXX,XXX bytes)
  MemoryCli.exe (X,XXX,XXX bytes)
  FileReaderCli.exe (X,XXX,XXX bytes)
  index.js (XXX bytes)
  package.json (XXX bytes)
  README.md (X,XXX bytes)
  MemoryCli说明.md (X,XXX bytes)
  FileReaderCli说明.md (XXX bytes)

Package size:
  Total: XX.XX MB
```

### npm 发布流程

**前置条件**:
1. 已安装 Node.js 和 npm
2. 已注册 npm 账号
3. 已登录 npm (`npm login`)
4. 构建脚本执行成功

**发布步骤**:

```powershell
# 步骤 1: 进入发布目录
cd publish

# 步骤 2: 验证包内容
npm pack --dry-run

# 步骤 3: 发布到 npm
npm publish

# 步骤 4: 验证发布成功
npm view @jingjingbox/mcp-cli-bridge
```

**版本更新流程**:

1. 更新 `package.json` 中的 `version` 字段
2. 更新 `AGENTS.md` 中的版本号
3. 执行 `.\build.ps1` 重新构建
4. 执行 `npm publish` 发布新版本

**发布平台**: Windows x64

**注意事项**:
- 发布前确保所有测试通过
- 检查版本号是否正确更新
- 确认 `publish/` 目录包含所有必需文件
- 首次发布后无法删除包，只能发布新版本

***

## 环境配置

### 环境变量

| 变量                | 描述               | 必填    |
| ----------------- | ---------------- | ----- |
| `MCP_MEMORY_PATH` | MemoryCli 数据存储目录 | **是** |

### 配置示例

**全局安装**:

```json
{
  "mcpServers": {
    "cli-bridge": {
      "type": "stdio",
      "command": "mcp-cli-bridge",
      "enabled": true,
      "env": {
        "MCP_MEMORY_PATH": "D:\\MCP\\Memory"
      }
    }
  }
}
```

**npx 方式**:

```json
{
  "mcpServers": {
    "cli-bridge": {
      "type": "stdio",
      "command": "npx",
      "args": ["@jingjingbox/mcp-cli-bridge"],
      "enabled": true,
      "env": {
        "MCP_MEMORY_PATH": "D:\\MCP\\Memory"
      }
    }
  }
}
```

***

## 开发流程

### 标准流程

1. **编译**: `dotnet build McpHost.slnx -c Release`
2. **测试**: `dotnet test McpHost.slnx -c Release`
3. **E2E**: `dotnet run --project tests\E2E\MyMemoryServer.E2E.csproj -c Release`
4. **发布**: `.\build.ps1`
5. **推送**: `git push origin main`

### 代码审查要点

- [ ] AOT 兼容性检查
- [ ] JSON 类型化检查
- [ ] GlobalUsings 检查
- [ ] Contracts 纯度检查
- [ ] CLI 隔离检查
- [ ] 参数封装检查
- [ ] 硬编码检查
- [ ] 异常处理检查
- [ ] 性能监控埋点

***

## 常见问题

### 1. AOT 编译失败

**原因**: 使用了运行时反射或动态代码生成\
**解决**: 改用源码生成器或直接写死

### 2. JSON 解析错误

**原因**: 直接解析 JSON 字符串\
**解决**: 创建类型化的类并使用 JsonContext

### 3. CLI 插件加载失败

**原因**: CLI 服务引用了 McpHost\
**解决**: CLI 只能引用 Common 和 Common.Contracts

### 4. 进程池耗尽

**原因**: CLI 进程未正确释放\
**解决**: 检查超时配置和异常处理

***

## 性能优化

### GC 优化

- 字符串操作使用 `Span<T>`
- 集合使用 `HashSet`/`Dictionary`
- 忽略大小写使用 `StringComparer.OrdinalIgnoreCase`
- AOT 优化使用 `FrozenDictionary`

### LINQ 链式编程

**推荐**:

```csharp
var results = source
    .Where(x => x > 0)
    .Select(x => x * 2)
    .ToList();
```

**避免**:

```csharp
var results = new List<int>();
foreach (var item in source)
{
    if (item > 0)
        results.Add(item * 2);
}
```

***

## 安全规范

### 1. 文件访问控制

- 多进程使用 `FileShare.Read` 只读共享
- 多线程使用 `ReaderWriterLockSlim`
- 所有操作传入 `CancellationToken`

### 2. 超时控制

- 文件 IO: 30 秒
- 锁获取: 10 秒
- 插件加载: 60 秒
- 消息队列: 30 秒

### 3. 异常处理

- 每个外部请求 try-catch
- 记录详细日志和栈帧
- 不吞掉异常

***

## 维护规范

### 文件管理

- 删除改为移动到 `.x/` 目录
- 修改 `.gitignore` 排除 `.x/`
- 记录旧文件修改

### 代码合并

- 合并重复代码
- 删除 `Obsolete` 标记
- 保持工程简洁

### 依赖管理

- 引入外部组件前先抽象接口
- 避免全局替换

***

## 技术债务预防

| 检查项          | 要求                         |
| ------------ | -------------------------- |
| AOT 编译       | 所有项目支持 NativeAOT           |
| JSON 处理      | 必须用类表示                     |
| GlobalUsings | .cs 文件禁止 using             |
| Contracts 项目 | 只能有接口/抽象                   |
| CLI 服务隔离     | 不能直接引用 McpHost             |
| 参数封装         | 避免多参数方法                    |
| 禁止硬编码        | 使用字典/特性替代                  |
| 超时控制         | 所有异步操作传入 CancellationToken |
| 异常处理         | 每个外部请求 try-catch 并记录日志     |
| 性能监控         | 关键路径埋点                     |
| 基建项目         | 共享功能放 Common               |
| 服务项目         | CLI 输出 EXE                 |
| 独立 NuGet     | MCP 协议独立包                  |

***

## 联系方式

**作者**: JJbox\
**仓库**: <https://gitee.com/JJbox/memory.git>\
**问题反馈**: <https://gitee.com/JJbox/memory/issues>\
**许可证**: MIT
