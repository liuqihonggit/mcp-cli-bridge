# McpHost 项目核心规则

> 📖 **详细文档**: [README.md](./README.md)（架构设计、使用方式、问题排查）

## 📁 项目结构

### 目录树(关键部分)

```
McpHost/
├── McpHost.slnx                    # 解决方案文件
├── build.ps1                       # 本地构建脚本（AI智能体唯一可用的构建脚本）
├── buildAndNpm.ps1                 # CI/CD 专属发布脚本（⛔ AI智能体禁止使用）
├── release.ps1                     # 版本发布脚本（用户明确要求时 AI 可执行）
├── package.json                    # npm 包配置
│
├── src/                            # 源代码
│   ├── Common/                     # 共享库（DLL）
│   ├── Common.Contracts/           # 契约层（DLL）
│   ├── McpHost/                    # 主程序（EXE）
│   └── Plugins/                    # 外部插件（EXE）
│       ├── FileReaderCli/          # 文件读取 CLI
│       └── MemoryCli/              # 内存存储 CLI
│
├── lib/                            # 外部依赖
│   └── McpProtocol/                # MCP 协议库
│
├── tests/                          # 测试
│   ├── Benchmarks/                 # 性能测试
│   ├── E2E/                        # 端到端测试
│   ├── SecurityTests/              # 安全测试
│   └── UnitTests/                  # 单元测试
│
├── nuget.config                    # NuGet 配置
├── .github/workflows/              # CI/CD
└── AI交互文档/                      # AI 开发规范
```

## ⚡ 关键约束（修改代码前必读）

1. ❌ **禁止缓存CLI工具列表**（未来可能上万命令）
2. ✅ 必须通过 `build.ps1` 发布到 `publish/`（`buildAndNpm.ps1` 是 CI/CD 专属，AI智能体禁止使用）
3. 🔒 `src/Plugins/` 是外部插件，内部项目不得直接引用
4. 🎯 MCP Server 只暴露 Host 层工具，CLI工具通过 `tool_execute` 调用
5. 🚫 **必须支持 NativeAOT 编译**
   - 所有代码必须围绕 NativeAOT 编译构造
   - 否则 npm 包体积过大导致安装崩溃
   - 这是不可变原则
   - 检查项：动态类型、反射 emit、直接解析 JSON 均会导致 AOT 失败

## 🏗️ 架构依赖关系

```
Plugins (EXE) ──→ Common (DLL) ──→ Common.Contracts (DLL)
                         ↑
McpHost (EXE) ───────────┘     McpProtocol (DLL/NuGet)
```

| 组件                   | 类型  | 约束                                             |
| -------------------- | --- | ---------------------------------------------- |
| **McpHost**          | EXE | MCP服务器主机                                       |
| **Common**           | DLL | 共享基建                                           |
| **Common.Contracts** | DLL | ⚠️ 禁止添加实现（只能有接口/DTO）                           |
| **Plugins/**         | EXE | ⚠️ 不能引用 McpHost，只能引用 Common + Common.Contracts |

## � 项目特定架构约束

### CLI 服务隔离（强制）

- **✅ CLI 服务不能直接引用 McpHost 项目**
- **✅ CLI 服务只能引用 Common、Common.Contracts**
- **✅ 通过 CLI 协议通信，不直接调用代码**
- **原因**: 防止循环依赖，保持插件独立性

### 架构分离和解耦（强制）

- **✅ 修改代码要看相邻的服务/单元测试联动更改**
- **✅ 热衷于维护代码而不是新建类和方法**
- **✅ 遇到** **`Obsolete`** **标记直接删除，不要兼容旧代码**
- **原因**: 这是新工程，不需要向后兼容

### 异步编程适用场景（项目特定）

**✅ 库代码必须使用** **`ConfigureAwait(false)`**

- **适用项目**: Common、Common.Contracts、Plugins 等库项目
- **原因**: 避免捕获 SynchronizationContext，减少 15-20% 延迟
- **例外**: McpHost 主程序（如需更新 UI 可省略）

```csharp
// ✅ 库代码标准写法（Common/Contracts/Plugins）
var result = await GetDataAsync(ct).ConfigureAwait(false);

// ⚠️ 主程序可省略（McpHost）
var result = await GetDataAsync(ct);
```

## �🔨 构建发布规则

### 命令

```powershell
# 本地构建（AI智能体唯一可用的构建脚本）
.\build.ps1

# ⛔ CI/CD 专属（AI智能体禁止使用）
# .\buildAndNpm.ps1

# 开发流程
dotnet build McpHost.slnx -c Release        # 编译
dotnet test McpHost.slnx -c Release          # 测试
dotnet run --project tests\E2E\MyMemoryServer.E2E.csproj -c Release  # E2E
.\build.ps1                                 # 本地构建
git push origin main                         # 推送

# ⚠️ 注意：npm publish 由 CI/CD 自动执行，禁止手动操作！
```

### 📦 构建脚本说明

| 脚本 | 使用者 | 执行内容 | AI智能体可用 |
| --- | --- | --- | --- |
| `build.ps1` | 开发者 / AI智能体 | AOT构建所有项目 → 输出到 `publish/` | ✅ 可用 |
| `buildAndNpm.ps1` | CI/CD 流水线 | 调用 `build.ps1` → 复制 npm 文件 → `npm publish` | ⛔ 禁止使用 |
| `release.ps1` | 开发者 / AI（用户触发） | 递增版本号 → 更新 package.json / Directory.Build.props → git commit + tag → **输出推送命令（由用户手动执行）** → 清理 npm 缓存 | ⚠️ 仅用户明确要求时 |

### ⚠️ 强制规则

- **❌ 禁止手动复制文件**到 `publish/` 目录，必须通过 `build.ps1`
- **⛔ 禁止执行 `buildAndNpm.ps1`**: 该脚本仅供 CI/CD 流水线使用，AI智能体不得调用
- **⚠️ `release.ps1` 仅用户触发时执行**: AI智能体不得自主执行，但当用户使用以下触发词时必须执行：
  - 触发词：`发布npm`、`发布新的npm`、`发布新版本`、`release`、`发版`
  - 执行方式：`.\release.ps1 [-VersionBump patch|minor|major]`（默认 patch）
  - **脚本行为**: 自动完成版本号递增 → 更新文件 → git commit + tag → 输出推送命令
  - **⛔ AI智能体禁止执行 git push**: 脚本输出推送命令后，由用户手动执行 `git push` 确保网络通畅
  - 用户需手动执行的推送命令:
    ```powershell
    # 推送到 Gitee
    git push origin main && git push origin v{新版本号}
    # 推送到 GitHub (触发 CI/CD)
    git push github main && git push github v{新版本号}
    ```
  - 执行后向用户报告：新版本号、待执行的推送命令、CI/CD 监控链接
- **⛔ 禁止手动执行 `npm publish`**: 正式发布由 CI/CD 自动完成，AI智能体不得手动发布
- **Git提交顺序**: CI/CD 发布成功 → git commit → git push
- **❌ 禁止在 CI/CD 发布成功前 commit**
- **⛔ 禁止并行子智能体期间提交 Git**: 指派子智能体时必须告知"当前处于并行期间，禁止 git commit/push"，由主智能体统一提交
- **⚠️ Git 提交前遇到错误禁止强行提交**: 可能是并行子智能体正在操作同一仓库，应等待并行任务完成后再统一处理

## 🌐 环境变量

| 变量                |  必填 | 说明               |
| ----------------- | :-: | ---------------- |
| `MCP_MEMORY_PATH` | ✅ 是 | MemoryCli 数据存储目录 |

## 🚨 常见问题速查

| 问题      | 原因          | 解决方案                               |
| ------- | ----------- | ---------------------------------- |
| CLI加载失败 | 引用了 McpHost | 移除引用，只依赖 Common + Common.Contracts |
| 进程池耗尽   | 超时配置不当      | 检查超时配置，确保进程正确释放                    |
| npm发布失败 | 版本已存在 | 递增 package.json 版本号，通过 CI/CD 重新发布 |
| AOT编译失败 | 使用了不兼容特性    | 检查：动态类型、反射emit、直接解析JSON            |

