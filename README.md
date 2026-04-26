# @jingjingbox/mcp-cli-bridge

MCP-CLI 桥接服务器 - 通过 MCP 协议调用 CLI 工具。

## 架构 (v3.0 渐进式发现)

```
┌─────────────┐     ┌─────────────────┐     ┌─────────────┐
│   Trae IDE  │────▶│    McpHost      │────▶│  MemoryCli  │
│ (MCP Client)│◄────│  (MCP Server)   │◄────│  (CLI Tool) │
└─────────────┘     └─────────────────┘     └─────────────┘
                         │
            ┌────────────┼────────────┐
            ▼            ▼            ▼
       tool_list   tool_describe  tool_execute
       (插件摘要)  (按需获取详情)   (执行命令)
```

- **McpHost**: MCP 服务器入口，只暴露 Host 层管理工具（`tool_list`/`tool_describe`/`tool_search`/`tool_execute`）
- **MemoryCli / FileReaderCli**: 纯 CLI 工具，内部命令不直接暴露给 MCP
- **渐进式发现**: LLM 先看插件摘要，需要时再拉取具体命令，降低上下文成本

## 安装

### 从 NPM 安装 (推荐)

```bash
npm install -g @jingjingbox/mcp-cli-bridge
```

或直接运行：

```bash
npx @jingjingbox/mcp-cli-bridge
```

### 从源码构建

```bash
git clone https://gitee.com/JJbox/memory.git
cd memory
dotnet publish src/McpHost/McpHost.csproj -c Release
dotnet publish src/Plugins/MemoryCli/MemoryCli.csproj -c Release
```

## 配置

### 方式一：全局安装（推荐）

```bash
npm install -g @jingjingbox/mcp-cli-bridge
```

**Trae / VS Code / Cursor MCP 设置：**

```json
{
  "mcpServers": {
    "cli-bridge": {
      "type": "stdio",
      "command": "mcp-cli-bridge",
      "enabled": true
    }
  }
}
```

### 方式二：项目本地安装

```bash
npm install @jingjingbox/mcp-cli-bridge
```

**MCP 设置（使用 node 启动）：**

```json
{
  "mcpServers": {
    "cli-bridge": {
      "type": "stdio",
      "command": "node",
      "args": ["./node_modules/@jingjingbox/mcp-cli-bridge/index.js"],
      "enabled": true
    }
  }
}
```

**Windows 也可直接用 exe：**

```json
{
  "mcpServers": {
    "cli-bridge": {
      "type": "stdio",
      "command": "./node_modules/@jingjingbox/mcp-cli-bridge/McpHost.exe",
      "enabled": true
    }
  }
}
```

### 方式三：npx（无需安装）

```json
{
  "mcpServers": {
    "cli-bridge": {
      "type": "stdio",
      "command": "npx",
      "args": ["@jingjingbox/mcp-cli-bridge"],
      "enabled": true
    }
  }
}
```

## 工作原理 (v3.0 渐进式发现)

1. **插件发现**: LLM 调用 `tool_list` 获取已注册插件摘要（名称/描述/命令数）
2. **按需详情**: LLM 调用 `tool_describe(pluginName)` 按需获取某插件的完整命令列表
3. **工具搜索**: LLM 调用 `tool_search(query)` 按关键词搜索可用插件
4. **工具执行**: LLM 调用 `tool_execute(tool, parameters)` 间接执行 CLI 内部工具
5. **参数传递**: 参数通过 JSON 对象传递，内部 Base64 编码给 CLI
6. **结果返回**: CLI 输出 JSON 结果，通过 MCP 返回给 LLM

## 参数传递说明

### MCP 工具调用格式

当 LLM 调用 MCP 工具时，需要遵循以下 JSON 格式：

```json
{
  "tool": "工具名称",
  "parameters": {
    "参数名1": "参数值1",
    "参数名2": "参数值2"
  }
}
```

### 嵌套参数说明

`tool_execute` 工具的 `parameters` 字段是一个嵌套的 JSON 对象，包含两个层级：

**第一层** - `tool_execute` 的参数：
```json
{
  "tool": "memory_create_entities",  // 要调用的 CLI 工具名称
  "parameters": { ... },              // CLI 工具的参数（第二层）
  "async": false,                     // 可选：是否异步
  "stream": false                     // 可选：是否流式
}
```

**第二层** - CLI 工具的参数（放在 `parameters` 字段内）：
```json
{
  "command": "create_entities",
  "entities": [...]
}
```

### 常见错误

1. **混淆两层参数**: 错误地将 CLI 参数直接放在第一层
   ```json
   // ❌ 错误
   {
     "tool": "memory_create_entities",
     "command": "create_entities"  // 应该放在 parameters 内
   }
   
   // ✅ 正确
   {
     "tool": "memory_create_entities",
     "parameters": {
       "command": "create_entities",
       "entities": [...]
     }
   }
   ```

2. **缺少 command 字段**: 所有 CLI 工具都需要 `command` 字段指定操作类型
   ```json
   // ❌ 错误 - 缺少 command
   {
     "entities": [...]
   }
   
   // ✅ 正确
   {
     "command": "create_entities",
     "entities": [...]
   }
   ```

3. **参数类型错误**: 确保数组类型参数传递数组，不是字符串
   ```json
   // ❌ 错误
   {
     "names": "实体名称"  // 应该是数组
   }
   
   // ✅ 正确
   {
     "names": ["实体名称"]
   }
   ```

## 可用工具

### MCP 层工具 (Host 层 - 仅暴露这些)

| 工具 | 描述 | 参数 |
|------|------|------|
| `tool_list` | 列出所有已注册插件（只显示摘要） | 无参数 |
| `tool_describe` | 按需获取某插件的完整命令列表 | `pluginName`(string,必填) |
| `tool_search` | 按关键词搜索可用插件 | `query`(string,必填), `limit`(int,可选) |
| `tool_execute` | 执行 CLI 内部工具（唯一调用入口） | `tool`(string,必填), `parameters`(object,必填) |
| `provider_list` | 列出所有已注册的提供者 | 无参数 |
| `package_status` | 检查包安装状态 | `packageName`(string,可选) |
| `package_install` | 安装/更新 CLI 工具包 | `packageName`(string,必填) |

### CLI 内部工具（不直接暴露，通过 tool_execute 间接调用）

以下命令**不会出现在 MCP tools/list 中**，LLM 需先通过 `tool_describe` 获取：

#### MemoryCli 内部命令

| 命令 | 描述 |
|------|------|
| `memory_create_entities` | 创建知识图谱实体 |
| `memory_create_relations` | 创建实体间关系 |
| `memory_read_graph` | 读取完整知识图谱 |
| `memory_search_nodes` | 搜索节点 |
| `memory_add_observations` | 添加观察记录 |
| `memory_delete_entities` | 删除实体 |
| `memory_open_nodes` | 获取指定节点 |

#### FileReaderCli 内部命令

| 命令 | 描述 |
|------|------|
| `file_reader_read_head` | 读取文件前 N 行 |
| `file_reader_read_tail` | 读取文件后 N 行 |

## 使用示例

### MCP 层工具调用

#### 1. tool_list - 列出已注册插件

**调用：**
```json
{
  "tool": "tool_list",
  "parameters": {}
}
```

**返回示例：**
```json
{
  "totalPlugins": 2,
  "plugins": [
    {
      "name": "memory",
      "description": "Knowledge Graph CLI - Manage entities, relations, and observations",
      "category": "knowledge-graph",
      "commandCount": 7,
      "hasDocumentation": true
    },
    {
      "name": "file_reader",
      "description": "File Reader CLI - Read file contents (head/tail)",
      "category": "file-operations",
      "commandCount": 2,
      "hasDocumentation": true
    }
  ]
}
```

---

#### 2. tool_describe - 按需获取插件命令详情

**参数：**
| 参数 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `pluginName` | string | 是 | 插件名称（如 `memory` 或 `file_reader`）|

**调用示例：**
```json
{
  "tool": "tool_describe",
  "parameters": {
    "pluginName": "memory"
  }
}
```

**返回示例（包含完整 7 个内部命令）：**
```json
{
  "pluginName": "memory",
  "description": "Knowledge Graph CLI",
  "commands": [
    {
      "name": "memory_create_entities",
      "description": "Create multiple new entities in the knowledge graph",
      "inputSchema": { ... }
    },
    { "name": "memory_create_relations", ... },
    { "name": "memory_read_graph", ... },
    ...
  ]
}
```

---

#### 3. tool_search - 搜索可用插件

**参数：**
| 参数 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `query` | string | 是 | 搜索关键词 |
| `limit` | int | 否 | 最大返回数量，默认10 |

**调用示例：**
```json
{
  "tool": "tool_search",
  "parameters": {
    "query": "memory",
    "limit": 5
  }
}
```

---

#### 4. tool_execute - 执行 CLI 工具

**参数：**
| 参数 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `tool` | string | 是 | 工具名称 |
| `parameters` | object | 是 | 工具参数(JSON对象) |
| `async` | bool | 否 | 是否异步执行，默认false |
| `stream` | bool | 否 | 是否流式输出，默认false |

**调用示例：**
```json
{
  "tool": "tool_execute",
  "parameters": {
    "tool": "memory_create_entities",
    "parameters": {
      "command": "create_entities",
      "entities": [
        {
          "name": "张三",
          "entityType": "person",
          "observations": ["喜欢编程", "使用C#"]
        }
      ]
    }
  }
}
```

---

#### 4. package_status - 检查包安装状态

**调用示例：**
```json
{
  "tool": "package_status"
}
```

---

#### 5. package_install - 安装 CLI 工具包

**参数：**
| 参数 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `packageName` | string | 是 | NPM包名 |
| `version` | string | 否 | 包版本 |

**调用示例：**
```json
{
  "tool": "package_install",
  "parameters": {
    "packageName": "@jingjingbox/memory-cli",
    "version": "1.0.0"
  }
}
```

---

### CLI 层工具调用 (通过 tool_execute)

#### 1. memory_create_entities - 创建实体

**参数结构：**
```json
{
  "command": "create_entities",
  "entities": [
    {
      "name": "实体名称",
      "entityType": "实体类型",
      "observations": ["观察记录1", "观察记录2"]
    }
  ]
}
```

**完整调用示例：**
```json
{
  "tool": "tool_execute",
  "parameters": {
    "tool": "memory_create_entities",
    "parameters": {
      "command": "create_entities",
      "entities": [
        {
          "name": "MyMemoryServer",
          "entityType": "project",
          "observations": [
            "MCP-CLI桥接服务器项目",
            "使用.NET 10开发",
            "支持知识图谱记忆功能"
          ]
        },
        {
          "name": "C#",
          "entityType": "technology",
          "observations": ["主要开发语言", "使用AOT编译"]
        }
      ]
    }
  }
}
```

---

#### 2. memory_create_relations - 创建关系

**参数结构：**
```json
{
  "command": "create_relations",
  "relations": [
    {
      "from": "源实体名称",
      "to": "目标实体名称",
      "relationType": "关系类型"
    }
  ]
}
```

**完整调用示例：**
```json
{
  "tool": "tool_execute",
  "parameters": {
    "tool": "memory_create_relations",
    "parameters": {
      "command": "create_relations",
      "relations": [
        {
          "from": "MyMemoryServer",
          "to": "C#",
          "relationType": "uses"
        },
        {
          "from": "MyMemoryServer",
          "to": "MCP",
          "relationType": "implements"
        }
      ]
    }
  }
}
```

---

#### 3. memory_read_graph - 读取完整图谱

**参数结构：**
```json
{
  "command": "read_graph"
}
```

**完整调用示例：**
```json
{
  "tool": "tool_execute",
  "parameters": {
    "tool": "memory_read_graph",
    "parameters": {
      "command": "read_graph"
    }
  }
}
```

**返回结构：**
```json
{
  "success": true,
  "data": {
    "entities": [...],
    "relations": [...]
  }
}
```

---

#### 4. memory_search_nodes - 搜索节点

**参数结构：**
```json
{
  "command": "search_nodes",
  "query": "搜索关键词"
}
```

**完整调用示例：**
```json
{
  "tool": "tool_execute",
  "parameters": {
    "tool": "memory_search_nodes",
    "parameters": {
      "command": "search_nodes",
      "query": "C#"
    }
  }
}
```

---

#### 5. memory_add_observations - 添加观察记录

**参数结构：**
```json
{
  "command": "add_observations",
  "name": "实体名称",
  "observations": ["新观察记录1", "新观察记录2"]
}
```

**完整调用示例：**
```json
{
  "tool": "tool_execute",
  "parameters": {
    "tool": "memory_add_observations",
    "parameters": {
      "command": "add_observations",
      "name": "C#",
      "observations": [
        "支持LINQ链式编程",
        "支持async/await异步编程"
      ]
    }
  }
}
```

---

#### 6. memory_delete_entities - 删除实体

**参数结构：**
```json
{
  "command": "delete_entities",
  "names": ["实体名称1", "实体名称2"]
}
```

**完整调用示例：**
```json
{
  "tool": "tool_execute",
  "parameters": {
    "tool": "memory_delete_entities",
    "parameters": {
      "command": "delete_entities",
      "names": ["旧项目", "废弃实体"]
    }
  }
}
```

---

#### 7. memory_open_nodes - 获取指定节点

**参数结构：**
```json
{
  "command": "open_nodes",
  "names": ["实体名称1", "实体名称2"]
}
```

**完整调用示例：**
```json
{
  "tool": "tool_execute",
  "parameters": {
    "tool": "memory_open_nodes",
    "parameters": {
      "command": "open_nodes",
      "names": ["MyMemoryServer", "C#"]
    }
  }
}
```

---

### 完整对话流程示例 (v3.0 渐进式发现)

#### 场景：记录项目信息并查询

**步骤1 - 列出可用插件：**
```
用户：有哪些可用的工具？

AI调用：tool_list

返回：
- memory: Knowledge Graph CLI (7 commands)
- file_reader: File Reader CLI (2 commands)
```

**步骤2 - 按需获取 memory 插件详情：**
```
用户：memory 插件有哪些命令？

AI调用：tool_describe({ pluginName: "memory" })

返回 7 个命令：
- memory_create_entities: 创建实体
- memory_search_nodes: 搜索节点
- memory_read_graph: 读取图谱
...
```

**步骤3 - 创建实体（通过 tool_execute）：**
```
用户：记录一个叫"张三"的开发者，他喜欢C#

AI调用：tool_execute
{
  "tool": "memory_create_entities",
  "parameters": {
    "command": "create_entities",
    "entities": [{
      "name": "张三",
      "entityType": "developer",
      "observations": ["喜欢C#编程"]
    }]
  }
}

返回：Created 1 entities
```

**步骤3 - 搜索验证：**
```
用户：我之前提到的开发者是谁？

AI调用：tool_execute
{
  "tool": "memory_search_nodes",
  "parameters": {
    "command": "search_nodes",
    "query": "开发者"
  }
}

返回：找到实体"张三"，类型developer，观察记录：喜欢C#编程
```

**步骤4 - 添加更多观察：**
```
用户：他还会使用.NET

AI调用：tool_execute
{
  "tool": "memory_add_observations",
  "parameters": {
    "command": "add_observations",
    "name": "张三",
    "observations": ["熟悉.NET开发"]
  }
}
```

**步骤5 - 查看完整信息：**
```
用户：显示张三的所有信息

AI调用：tool_execute
{
  "tool": "memory_open_nodes",
  "parameters": {
    "command": "open_nodes",
    "names": ["张三"]
  }
}
```

## 项目结构

```
src/
├── McpHost/            # MCP-CLI 桥接服务器
│   ├── Tools/
│   ├── Services/
│   ├── Plugins/
│   └── Program.cs
├── Service.Mcp/        # MCP 协议库
├── Service.Json/       # JSON 服务
├── Common/             # 共享组件
└── Plugins/            # CLI 插件
    ├── MemoryCli/      # 记忆 CLI 工具
    └── FileReaderCli/  # 文件读取 CLI 工具
```

## 添加新的 CLI 工具

1. 创建新的 CLI 项目在 `src/Plugins/` 目录下
2. 实现 `--json-input <base64>` 参数处理
3. 在插件配置中添加工具定义
4. 重新编译 McpHost

## 环境变量

| 变量 | 描述 | 默认值 |
|------|------|--------|
| `MEMORY_FILE_PATH` | 记忆存储路径 | `D:\MCP\Memory\memory.jsonl` |

## License

MIT
