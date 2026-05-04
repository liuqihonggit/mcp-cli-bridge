# @jingjingbox/mcp-cli-bridge

MCP-CLI 桥接服务器 - 通过 MCP 协议调用 CLI 工具，为 AI 助手提供强大的知识图谱和文件读取能力。

## 目录

- [简介](#简介)
- [核心价值](#核心价值)
- [核心特性](#核心特性)
- [系统要求](#系统要求)
- [安装](#安装)
- [配置](#配置)
- [插件目录](#插件目录)
- [使用方式](#使用方式)
- [架构设计](#架构设计)
- [技术栈](#技术栈)
- [开发指南](#开发指南)
- [许可证](#许可证)

***

## 简介

`@jingjingbox/mcp-cli-bridge` 是一个基于 [Model Context Protocol (MCP)](https://modelcontextprotocol.io) 的桥接服务器，它将 CLI 工具的能力通过 MCP 协议暴露给 AI 助手（如 TraeCN）。

### 项目定位

| 维度       | 说明                          |
| -------- | --------------------------- |
| **目标平台** | Windows x64                 |
| **技术栈**  | .NET 10.0, NativeAOT, C# 13 |
| **通信协议** | MCP Protocol + JSON-RPC 2.0 |
| **设计理念** | 进程隔离、渐进式加载、零配置              |

## 核心价值

### 设计原则概览

| 原则           | 实现方式                                          | 优势                   |
| ------------ | --------------------------------------------- | -------------------- |
| **MCP 接口统一** | Host 层暴露少量管理工具，CLI 内部工具通过 `tool_execute` 间接调用 | 降低上下文成本，避免一次性加载上万个命令 |
| **零配置发现**    | 自动扫描同目录下的 CLI.exe 文件，无需配置文件                   | 即插即用，简化部署流程          |
| **进程隔离架构**   | 每个 CLI 插件运行在独立进程中，通过 JSON-RPC 2.0 通信          | 插件崩溃不影响主服务，安全隔离      |
| **渐进式加载**    | LLM 动态获取工具描述，按需探索系统能力                         | 支持未来上万级命令规模扩展        |

### 关键约束说明

| 约束项          | 规则                                                 | 详细文档                                         |
| ------------ | -------------------------------------------------- | -------------------------------------------- |
| **CLI 工具缓存** | 禁止缓存 CLI 内部工具列表和描述                                 | [AGENTS.md - 关键约束](./AGENTS.md)             |
| **构建发布**     | 必须通过 `build.ps1` 脚本发布到 `publish/` 目录               | [AGENTS.md - 构建发布规则](./AGENTS.md)          |
| **插件隔离**     | `src/Plugins/` 为外部插件，内部项目不得直接引用                    | [AGENTS.md - CLI 服务隔离](./AGENTS.md)         |
| **接口暴露**     | MCP Server 只暴露 Host 层工具，CLI 工具通过 `tool_execute` 调用 | [AGENTS.md - 架构依赖关系](./AGENTS.md)          |

> 💡 **为什么这样设计？**
>
> 传统方案会将所有工具定义静态缓存到上下文中，当工具数量达到万级时会导致：
>
> - 上下文窗口爆炸式增长
> - LLM 处理效率急剧下降
> - 内存占用过高
>
> 本项目采用**动态发现 + 渐进式加载**策略，LLM 只在需要时才获取具体工具详情。

***

## 核心特性

| 特性                 | 描述                       | 技术实现                      |
| ------------------ | ------------------------ | ------------------------- |
| 🧠 **知识图谱管理**      | 创建实体、建立关系、搜索节点，构建完整的知识网络 | MemoryCli 插件              |
| 📄 **文件读取**        | 高效读取文件头部或尾部内容，支持大文件处理    | FileReaderCli 插件          |
| 🔒 **进程隔离**        | CLI 插件运行在独立进程中，崩溃不影响主服务  | ProcessPool 管理            |
| ⚡ **NativeAOT 编译** | 极致性能，超小体积，快速启动           | .NET 10.0 AOT             |
| 🔄 **渐进式加载**       | 工具按需加载，不占用上下文空间          | tool\_describe 动态获取       |
| 🔍 **动态发现**        | LLM 通过 MCP 协议实时探索系统能力    | tool\_search / tool\_list |

***

## 系统要求

| 组件           | 版本要求        | 说明                  |
| ------------ | ----------- | ------------------- |
| **操作系统**     | Windows x64 | 当前仅支持 Windows 平台    |
| **Node.js**  | >= 14.0.0   | 用于运行 npm 包入口脚本      |
| **.NET 运行时** | 10.0 (已包含)  | NativeAOT 编译产物自带运行时 |

> ⚠️ **注意**: 本项目专为 TraeCN AI 助手设计，确保环境满足上述要求后再安装使用。

***

## 安装

### 方式一：全局安装（推荐用于长期使用）

```bash
npm install -g @jingjingbox/mcp-cli-bridge@latest
```

### 方式二：使用 npx（推荐用于临时使用）

无需预先安装，直接运行：

```bash
npx @jingjingbox/mcp-cli-bridge@latest
```

> ⚠️ **版本更新提示**
>
> - 如果之前安装过旧版本，`npm install -g` 不会自动更新，请加 `@latest` 强制获取最新版
> - npx 同理，如果全局已安装旧版，npx 会优先使用全局版本，加 `@latest` 可跳过全局缓存
> - 清理旧缓存：`npm uninstall -g @jingjingbox/mcp-cli-bridge && npm cache clean --force`

### 安装验证

安装成功后，可通过以下命令验证：

```bash
# 全局安装后 - 确认版本号
mcp-cli-bridge --version

# 或使用 npx
npx @jingjingbox/mcp-cli-bridge@latest --help

# 查看已安装版本
npm list -g @jingjingbox/mcp-cli-bridge
```

***

## 配置

### 环境变量

| 变量名               | 描述                   |  必填 | 示例值             |
| ----------------- | -------------------- | :-: | --------------- |
| `MCP_MEMORY_PATH` | MemoryCli 知识图谱数据存储目录 | ✅ 是 | `D:\MCP\Memory` |

#### 数据文件说明

设置 `MCP_MEMORY_PATH` 后，系统会自动创建以下数据文件：

| 文件名                      | 用途         | 格式         |
| ------------------------ | ---------- | ---------- |
| `memory.jsonl`           | 存储知识图谱实体数据 | JSON Lines |
| `memory_relations.jsonl` | 存储实体间关系数据  | JSON Lines |

***

### TraeCN 配置示例

#### 配置一：使用 npx（推荐）

```json
{
  "mcpServers": {
    "cli-bridge": {
      "type": "stdio",
      "command": "npx",
      "args": ["@jingjingbox/mcp-cli-bridge@latest"],
      "enabled": true,
      "env": {
        "MCP_MEMORY_PATH": "D:\\MCP\\Memory"
      }
    }
  }
}
```

#### 配置二：全局安装后使用

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

> 💡 **配置提示**
>
> - 请将 `MCP_MEMORY_PATH` 替换为您希望存储知识的实际路径
> - 建议使用绝对路径，避免相对路径导致的路径解析问题
> - 首次运行时会自动创建数据文件，无需手动创建

***

## 插件目录

本包包含以下 CLI 插件（通过 MCP 协议按需加载）：

| 插件名称              | 分类   | 功能描述                | 包含命令数 | 主要用途      |
| ----------------- | ---- | ------------------- | :---: | --------- |
| **MemoryCli**     | 知识图谱 | 实体、关系、观察记录的 CRUD 操作 |   7   | 构建和管理知识网络 |
| **FileReaderCli** | 文件操作 | 文件头部/尾部高效读取         |   2   | 快速预览文件内容  |

> 💡 **插件扩展性**
>
> - 未来会持续添加更多插件（如代码分析、API 调用等）
> - 新插件只需放入 `Plugins/` 目录即可自动被发现
> - 所有插件都遵循相同的 CLI 通信协议

### 插件通信协议

Host 与 CLI 插件之间采用 **JSON-RPC 2.0** 格式通信：

**请求格式**:

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

详细协议说明请查看：[AGENTS.md - CLI 服务隔离](./AGENTS.md)

***

## 使用方式

### 渐进式发现流程（推荐）

LLM 通过以下三步流程动态探索系统能力，避免一次性加载所有工具：

```
┌─────────────────────────────────────────────────────┐
│  Step 1: 发现阶段                                    │
│  tool_list / tool_search → 查看可用的 CLI 插件       │
└──────────────────────┬──────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────┐
│  Step 2: 描述获取阶段                                │
│  tool_describe / list_tools → 获取命令详情和参数Schema │
└──────────────────────┬──────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────┐
│  Step 3: 执行阶段                                   │
│  tool_execute → 执行具体的 CLI 工具命令              │
└─────────────────────────────────────────────────────┘
```

### 内置帮助系统

每个插件都支持自描述机制，提供多层级帮助信息：

| 层级        | 调用方式               | 输出格式        | 适用场景       |
| --------- | ------------------ | ----------- | ---------- |
| **MCP 层** | `tool_describe`    | JSON Schema | LLM 解析参数结构 |
| **CLI 层** | `list_tools` 命令    | 人类可读文本      | 开发者查看帮助    |
| **命令行**   | `MemoryCli.exe -h` | 用法摘要        | 快速参考       |

### Host 层管理工具

这些是 McpHost 直接暴露的管理接口（唯一暴露给 MCP 客户端的工具）：

| 工具名称              | 描述             | 参数说明           |
| ----------------- | -------------- | -------------- |
| `tool_search`     | 搜索可用的 CLI 工具   | 支持关键词模糊匹配      |
| `tool_list`       | 列出所有可用工具       | 返回插件及工具清单      |
| `tool_describe`   | 获取工具详细描述       | 返回 JSON Schema |
| `tool_execute`    | 执行指定的 CLI 工具命令 | 传入工具名和参数       |
| `package_status`  | 检查包安装状态        | 查看已安装插件        |
| `package_install` | 安装指定包          | 从注册源安装新插件      |
| `provider_list`   | 列出工具提供者信息      | 查看插件元数据        |

> ⚠️ **重要**: CLI 内部工具（如 `memory_create_entities`、`file_reader_read_head`）**不会直接暴露**给 MCP 客户端，必须通过 `tool_execute` 间接调用。

***

## 架构设计

### 项目目录结构

```
McpHost/
├── lib/                              # 📦 外部依赖库
│   └── McpProtocol/                  # MCP 协议实现（独立 NuGet 包）
│       ├── src/McpProtocol/          #    协议服务器核心
│       └── src/McpProtocol.Contracts/#    协议模型与常量
│
├── src/                              # 💻 源代码
│   │
│   ├── Common/                       # 🔧 共享基础设施
│   │   ├── Caching/                  #    缓存中间件
│   │   ├── CliProtocol/              #    CLI 通信协议
│   │   ├── Configuration/            #    配置管理
│   │   ├── IoC/                      #    轻量级容器
│   │   ├── Json/                     #    JSON 序列化（AOT 安全）
│   │   ├── Logging/                  #    日志框架
│   │   ├── Middleware/               #    中间件管道
│   │   ├── Plugins/                  #    工具注册系统
│   │   ├── Reflection/               #    方法调用器
│   │   ├── Security/                 #    安全验证体系
│   │   └── Tools/                    #    工具元数据
│   │
│   ├── Common.Contracts/             # 📋 契约层（接口 + DTO）
│   │   ├── Caching/                  #    缓存接口
│   │   ├── IoC/                      #    容器抽象
│   │   ├── Middleware/               #    中间件接口
│   │   ├── Models/                   #    数据传输对象
│   │   ├── Plugins/                  #    工具提供者接口
│   │   └── Security/                 #    安全接口
│   │
│   ├── McpHost/                      # 🎯 MCP 主机服务
│   │   ├── Middleware/               #    请求处理管道
│   │   ├── Plugins/                  #    CLI 插件管理
│   │   ├── ProcessPool/              #    进程池管理
│   │   ├── Services/                 #    包管理服务
│   │   └── Tools/                    #    Host 层工具暴露
│   │
│   └── Plugins/                      # 🔌 CLI 插件（外部进程）
│       ├── MemoryCli/                #    知识图谱插件
│       └── FileReaderCli/            #    文件读取插件
│
├── tests/                            # 🧪 测试套件
│   ├── Benchmarks/                   #    性能基准测试
│   ├── E2E/                          #    端到端测试
│   ├── SecurityTests/                #    安全专项测试
│   └── UnitTests/                    #    单元测试
│
├── index.js                          # npm 入口文件
├── package.json                      # npm 包配置
├── build.ps1                         # AOT 构建脚本
└── McpHost.slnx                      # 解决方案文件
```

### 核心组件职责

| 组件                   | 类型          | 职责                        | 依赖关系                       |
| -------------------- | ----------- | ------------------------- | -------------------------- |
| **McpProtocol**      | DLL (NuGet) | MCP 协议实现，可被其他项目复用         | 无外部依赖                      |
| **Common**           | DLL         | 共享基建：缓存、日志、安全、IoC、JSON序列化 | → Common.Contracts         |
| **Common.Contracts** | DLL         | 契约层：接口定义和纯 DTO（禁止包含实现）    | 无外部依赖                      |
| **McpHost**          | EXE         | MCP 服务器主机，工具暴露和进程管理       | → Common, McpProtocol      |
| **Plugins/**         | EXE         | CLI 插件，独立进程隔离运行           | → Common, Common.Contracts |

### 依赖关系图

```
Plugins (EXE) ──→ Common (DLL) ──→ Common.Contracts (DLL)
                        ↑
McpHost (EXE) ───────────┘     McpProtocol (DLL/NuGet)
```

> ⚠️ **架构约束**
>
> - **Plugins 不能引用 McpHost**: CLI 插件只能依赖 Common 和 Common.Contracts
> - **Common.Contracts 禁止实现**: 该项目只能包含接口、抽象类和纯 DTO
> - **通信必须通过协议**: 插件与 Host 之间的交互必须通过 JSON-RPC 2.0 协议
>
> 详细架构说明请查看：[AGENTS.md - 架构依赖关系](./AGENTS.md)

### 进程隔离机制

| 特性        | 实现方式                   | 保障     |
| --------- | ---------------------- | ------ |
| **独立进程**  | 每个 CLI 插件运行在单独的进程中     | 崩溃不传播  |
| **进程池管理** | ProcessPool 统一管理进程生命周期 | 资源可控   |
| **超时控制**  | 可配置的超时时间，超时自动终止        | 避免挂死   |
| **资源限制**  | 限制并发进程数和内存占用           | 防止资源耗尽 |

详细生命周期和安全机制请查看：[AGENTS.md - CLI 服务隔离](./AGENTS.md)

***

## 技术栈

| 技术                     | 版本    | 用途       | 选择理由            |
| ---------------------- | ----- | -------- | --------------- |
| **.NET**               | 10.0  | 运行时框架    | 最新特性，长期支持       |
| **NativeAOT**          | 内置    | 编译模式     | 极致性能，超小体积，快速启动  |
| **C#**                 | 13    | 编程语言     | 现代语法，高性能        |
| **JSON-RPC**           | 2.0   | CLI 通信协议 | 轻量级，易于实现        |
| **MCP Protocol**       | 最新    | AI 通信协议  | 行业标准，广泛支持       |
| **System.CommandLine** | NuGet | CLI 参数解析 | 支持 `-h` 帮助，类型安全 |

### 性能指标

| 指标         | 目标值     | 说明               |
| ---------- | ------- | ---------------- |
| **启动时间**   | < 100ms | NativeAOT 冷启动    |
| **包体积**    | < 10MB  | AOT 编译优化         |
| **内存占用**   | < 50MB  | 基础运行内存           |
| **工具调用延迟** | < 50ms  | 进程内执行（不含 CLI 启动） |

***

## 开发指南

### 源码仓库

本项目同时在 Gitee 和 GitHub 上维护：

| 平台         | 地址                                    | 访问特点          |
| ---------- | ------------------------------------- | ------------- |
| **Gitee**  | <https://gitee.com/JJbox/memory>      | 国内访问更快        |
| **GitHub** | <https://github.com/JJbox-io/McpHost> | 国际访问，Issue 跟踪 |

```bash
# 从 Gitee 克隆（国内推荐）
git clone https://gitee.com/JJbox/memory.git

# 或从 GitHub 克隆（国际推荐）
git clone https://github.com/JJbox-io/McpHost.git
```

### 开发环境准备

| 步骤             | 命令/操作                | 说明               |
| -------------- | -------------------- | ---------------- |
| 1. 克隆仓库        | `git clone <url>`    | 获取源代码            |
| 2. 安装 .NET SDK | 下载 .NET 10.0 SDK     | 开发编译必需           |
| 3. 配置环境变量      | 设置 `MCP_MEMORY_PATH` | 测试 MemoryCli 时需要 |
| 4. 验证安装        | `dotnet --version`   | 确保 SDK 可用        |

### 构建流程

```powershell
# 使用 PowerShell 执行构建脚本
.\build.ps1
```

> 💡 **构建脚本功能**
>
> `build.ps1` 会自动完成以下工作：
>
> 1. 清理 `publish/` 目录
> 2. AOT 编译所有项目（McpHost、MemoryCli、FileReaderCli）
> 3. 复制必要文件到 `publish/` 目录
> 4. 验证所有必需文件是否存在
>
> ⚠️ **重要**: 禁止手动复制文件到 `publish/` 目录！所有发布文件必须通过构建脚本自动复制。
>
> 详细构建流程、文件规则和错误处理请查看：[AGENTS.md - 构建发布规则](./AGENTS.md)

### 测试

| 测试类型       | 命令                                                                    | 说明       |
| ---------- | --------------------------------------------------------------------- | -------- |
| **单元测试**   | `dotnet test McpHost.slnx -c Release`                                 | 运行所有单元测试 |
| **E2E 测试** | `dotnet run --project tests\E2E\MyMemoryServer.E2E.csproj -c Release` | 端到端集成测试  |
| **性能测试**   | `dotnet run --project tests\Benchmarks -c Release`                    | 基准性能测试   |

### 标准开发流程

遵循以下标准开发流程以确保代码质量和稳定性：

```
1. 编译 → 2. 测试 → 3. E2E → 4. 发布 → 5. 推送
```

| 阶段      | 命令                                                                    | 验证点             |
| ------- | --------------------------------------------------------------------- | --------------- |
| **编译**  | `dotnet build McpHost.slnx -c Release`                                | 编译成功，无警告        |
| **测试**  | `dotnet test McpHost.slnx -c Release`                                 | 所有测试通过          |
| **E2E** | `dotnet run --project tests\E2E\MyMemoryServer.E2E.csproj -c Release` | 集成测试通过          |
| **发布**  | `.\build.ps1`                                                         | publish/ 目录生成正确 |
| **推送**  | `git push origin main`                                                | 代码同步到远程         |

> 📖 **详细开发规范**
>
> 本项目有严格的开发规范和行为准则，所有开发者必须遵守：
>
> - **[AGENTS.md](./AGENTS.md)** - 项目说明书
>   - 架构设计和组件职责
>   - 构建发布流程和规则
>   - CLI 插件系统和通信协议
>   - 环境配置要求
> - **[CLAUDE.md](./CLAUDE.md)** - AI 行为红线手册
>   - ❌ 绝对禁止的操作（代码编写禁令、操作禁令）
>   - ✅ 必须执行的开发流程和质量要求
>   - 🔄 强制性工作流程（经验复用、渐进式迁移）
>   - 🛡️ 安全红线和异常处理规范
>
> 在开始任何开发工作前，**务必先阅读这两份文档**！

### 代码质量要求

| 要求类别             | 具体规则                         | 参考文档                                         |
| ---------------- | ---------------------------- | -------------------------------------------- |
| **NativeAOT 兼容** | 禁止动态类型、反射 emit、动态代码生成        | [CLAUDE.md - 代码编写禁令](./CLAUDE.md#代码编写禁令)     |
| **GlobalUsings** | 禁止在 .cs 文件内写 using 语句        | [CLAUDE.md - 代码编写禁令](./CLAUDE.md#代码编写禁令)     |
| **类型安全**         | 禁止硬编码，使用 typeof()/nameof()   | [CLAUDE.md - 代码编写禁令](./CLAUDE.md#代码编写禁令)     |
| **参数封装**         | 方法参数不超过 3 个，多参数封装为类          | [CLAUDE.md - 代码质量强制要求](./CLAUDE.md#代码质量强制要求) |
| **异步编程**         | 所有异步操作必须传入 CancellationToken | [CLAUDE.md - 代码质量强制要求](./CLAUDE.md#代码质量强制要求) |
| **异常处理**         | 外部请求必须 try-catch 并记录日志       | [CLAUDE.md - 代码质量强制要求](./CLAUDE.md#代码质量强制要求) |
| **Git 规范**       | 先备份、无分页模式、npm 发布后才 commit    | [CLAUDE.md - 开发流程强制要求](./CLAUDE.md#开发流程强制要求) |

### 常见问题排查

| 问题现象           | 可能原因           | 解决方案                | 参考文档                                         |
| -------------- | -------------- | ------------------- | -------------------------------------------- |
| CLI 插件加载失败     | 引用了 McpHost 项目 | 移除对 McpHost 的引用     | [AGENTS.md - 常见问题速查](./AGENTS.md)     |
| 进程池耗尽          | 超时配置不当或异常未处理   | 检查超时配置和异常处理         | [AGENTS.md - 常见问题速查](./AGENTS.md)     |
| npm 发布失败版本已存在  | 版本号未递增         | 更新 package.json 版本号 | [AGENTS.md - 构建发布规则](./AGENTS.md)     |
| NativeAOT 编译失败 | 使用了不兼容的特性      | 检查是否使用了动态类型等        | [CLAUDE.md - 代码编写禁令](./CLAUDE.md#代码编写禁令)     |

***

## 许可证

本项目采用 [MIT](LICENSE) 许可证开源。

## 作者

**JJbox**

| 平台         | 链接                                           |
| ---------- | -------------------------------------------- |
| **Gitee**  | <https://gitee.com/JJbox/memory>             |
| **GitHub** | <https://github.com/JJbox-io/McpHost>        |
| **问题反馈**   | <https://github.com/JJbox-io/McpHost/issues> |

***

> 📚 **相关文档**
>
> - [AGENTS.md](./AGENTS.md) - 项目说明书（架构、构建、发布规范）
> - [CLAUDE.md](./CLAUDE.md) - AI 行为红线手册（开发规范、安全准则）
>
> 如需了解更详细的技术实现、开发流程或问题排查，请查阅以上文档。

