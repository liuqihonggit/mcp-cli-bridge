# @jingjingbox/mcp-cli-bridge
MCP-CLI 桥接服务器 - 通过 MCP 协议调用 CLI 工具，为 AI 助手提供强大的知识图谱和文件读取能力。

## 简介

`@jingjingbox/mcp-cli-bridge` 是一个基于 [Model Context Protocol (MCP)](https://modelcontextprotocol.io) 的桥接服务器，它将 CLI 工具的能力通过 MCP 协议暴露给 AI 助手（如 TraeCN）。

### 核心特性

- **知识图谱管理**: 创建实体、建立关系、搜索节点，构建完整的知识网络
- **文件读取**: 高效读取文件头部或尾部内容，支持大文件处理
- **进程隔离**: CLI 插件运行在独立进程中，崩溃不影响主服务
- **NativeAOT 编译**: 极致性能，超小体积，快速启动
- **渐进式加载**: 工具按需加载，不占用上下文空间

## 系统要求

- **操作系统**: Windows x64
- **Node.js**: >= 14.0.0
- **运行时**: .NET 10.0 Runtime (已包含在包内)

## 安装

### 全局安装

```bash
npm install -g @jingjingbox/mcp-cli-bridge
```

### 使用 npx（推荐）

无需安装，直接运行：

```bash
npx @jingjingbox/mcp-cli-bridge
```

## 配置

### 环境变量

| 变量 | 描述 | 必填 |
|------|------|------|
| `MCP_MEMORY_PATH` | MemoryCli 知识图谱数据存储目录 | **是** |

数据文件会自动创建：
- `memory.jsonl` - 实体数据
- `memory_relations.jsonl` - 关系数据

### TraeCN 配置示例

在 TraeCN 的 MCP 配置中添加：

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

### 全局安装配置

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

## 工具列表

### Host 层工具（直接暴露）

| 工具名 | 描述 |
|--------|------|
| `tool_search` | 搜索可用的 CLI 工具 |
| `tool_execute` | 执行指定的 CLI 工具命令 |
| `tool_describe` | 获取工具描述信息 |
| `tool_list` | 列出所有可用工具 |
| `package_status` | 检查包安装状态 |
| `package_install` | 安装指定包 |

### MemoryCli 工具（知识图谱）

通过 `tool_execute` 调用，命令前缀为 `memory_`：

| 工具名 | 描述 |
|--------|------|
| `memory_create_entities` | 创建多个实体到知识图谱 |
| `memory_create_relations` | 在实体之间创建关系 |
| `memory_read_graph` | 读取整个知识图谱 |
| `memory_search_nodes` | 搜索知识图谱中的节点 |
| `memory_add_observations` | 向实体添加观察记录 |
| `memory_delete_entities` | 删除实体及其关系 |
| `memory_open_nodes` | 按名称获取特定节点 |

### FileReaderCli 工具（文件读取）

通过 `tool_execute` 调用，命令前缀为 `file_reader_`：

| 工具名 | 描述 |
|--------|------|
| `file_reader_read_head` | 读取文件前 N 行 |
| `file_reader_read_tail` | 读取文件后 N 行 |

## 使用示例

### 创建知识实体

```json
{
  "command": "memory_create_entities",
  "entities": [
    {
      "name": "React",
      "entityType": "Framework",
      "observations": ["用于构建用户界面的 JavaScript 库", "由 Meta 维护"]
    },
    {
      "name": "TypeScript",
      "entityType": "Language",
      "observations": ["JavaScript 的超集", "添加了静态类型检查"]
    }
  ]
}
```

### 建立实体关系

```json
{
  "command": "memory_create_relations",
  "relations": [
    {
      "from": "React",
      "to": "TypeScript",
      "relationType": "supports"
    }
  ]
}
```

### 搜索知识节点

```json
{
  "command": "memory_search_nodes",
  "query": "React"
}
```

### 读取文件头部

```json
{
  "command": "file_reader_read_head",
  "filePath": "C:\\project\\logs\\app.log",
  "lineCount": 20
}
```

### 读取文件尾部

```json
{
  "command": "file_reader_read_tail",
  "filePath": "C:\\project\\logs\\error.log",
  "lineCount": 50
}
```

## 架构设计

```
McpHost/
├── McpHost.exe          # MCP 主机服务
├── MemoryCli.exe        # 知识图谱 CLI 插件
├── FileReaderCli.exe    # 文件读取 CLI 插件
└── index.js             # npm 入口文件
```

- **McpHost**: MCP 协议服务器，负责工具暴露和 CLI 进程管理
- **MemoryCli**: 知识图谱管理，支持实体、关系、观察记录的 CRUD
- **FileReaderCli**: 高效文件读取，支持大文件的头部/尾部读取

## 技术栈

- **.NET 10.0** - 运行时平台
- **NativeAOT** - 原生编译，极致性能
- **C# 13** - 编程语言
- **JSON-RPC 2.0** - CLI 通信协议
- **MCP Protocol** - AI 助手通信协议

## 开发

### 源码仓库

```bash
git clone https://gitee.com/JJbox/memory.git
```

### 构建

```powershell
# 使用 PowerShell
.\build.ps1
```

### 测试

```bash
# 单元测试
dotnet test McpHost.slnx

# E2E 测试
dotnet run --project tests\E2E\MyMemoryServer.E2E.csproj -c Release
```

## 详细文档

- [MemoryCli 使用说明](./MemoryCli说明.md) - 知识图谱完整 API 文档
- [FileReaderCli 使用说明](./FileReaderCli说明.md) - 文件读取完整 API 文档

## 许可证

[MIT](LICENSE)

## 作者

**JJbox**

- 仓库: https://gitee.com/JJbox/memory
- 问题反馈: https://gitee.com/JJbox/memory/issues
