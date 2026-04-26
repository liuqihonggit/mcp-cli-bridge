# @jingjingbox/mcp-cli-bridge

MCP-CLI 桥接服务器 - 通过 MCP 协议调用 CLI 工具。

## 架构

```
┌─────────────┐     ┌─────────────────┐     ┌─────────────┐
│   Trae IDE  │────▶│    McpHost      │────▶│  MemoryCli  │
│ (MCP Client)│◄────│  (MCP Server)   │◄────│  (CLI Tool) │
└─────────────┘     └─────────────────┘     └─────────────┘
                            │
                            ▼
                     ┌─────────────┐
                     │  tool.json  │
                     │ (工具定义)   │
                     └─────────────┘
```

- **McpHost**: MCP 服务器入口，暴露 `tool_search` 和 `tool_execute` 工具
- **MemoryCli**: 纯 CLI 工具，提供知识图谱记忆功能
- **tools.json**: 定义可用的 CLI 工具及其参数

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

### Trae / VS Code / Cursor

添加到 MCP 设置：

```json
{
  "mcpServers": {
    "cli-bridge": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\path\\to\\MyMemoryServer\\src\\McpHost\\McpHost.csproj"
      ],
      "enabled": true
    }
  }
}
```

或使用已发布的可执行文件：

```json
{
  "mcpServers": {
    "cli-bridge": {
      "type": "stdio",
      "command": "C:\\path\\to\\publish\\McpHost.exe",
      "enabled": true
    }
  }
}
```

## 工作原理

1. **工具发现**: LLM 调用 `tool_search` 查询可用的 CLI 工具
2. **工具执行**: LLM 调用 `tool_execute` 执行指定的 CLI 工具
3. **参数传递**: 参数通过 Base64 编码的 JSON 传递给 CLI
4. **结果返回**: CLI 输出 JSON 结果，通过 MCP 返回给 LLM

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

### MCP 层工具 (McpHost)

| 工具 | 描述 | 参数 |
|------|------|------|
| `tool_search` | 搜索可用的 CLI 工具 | `query`(string,必填), `limit`(int,可选) |
| `tool_get` | 获取指定工具的详细信息 | `name`(string,必填) |
| `tool_execute` | 执行指定的 CLI 工具 | `tool`(string,必填), `parameters`(object,必填), `async`(bool,可选), `stream`(bool,可选) |
| `package_status` | 检查 CLI 包安装状态 | 无参数 |
| `package_install` | 安装/更新 CLI 工具包 | `packageName`(string,必填), `version`(string,可选) |

### CLI 层工具 (MemoryCli)

| 工具 | 描述 |
|------|------|
| `memory_create_entities` | 创建知识图谱实体 |
| `memory_create_relations` | 创建实体间关系 |
| `memory_read_graph` | 读取完整知识图谱 |
| `memory_search_nodes` | 搜索节点 |
| `memory_add_observations` | 添加观察记录 |
| `memory_delete_entities` | 删除实体 |
| `memory_open_nodes` | 获取指定节点 |

## 使用示例

### MCP 层工具调用

#### 1. tool_search - 搜索可用工具

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

**返回示例：**
```json
[
  {
    "name": "memory_create_entities",
    "description": "Create multiple new entities in the knowledge graph",
    "requiredParams": ["command", "entities"]
  }
]
```

---

#### 2. tool_get - 获取工具详情

**参数：**
| 参数 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `name` | string | 是 | 工具名称 |

**调用示例：**
```json
{
  "tool": "tool_get",
  "parameters": {
    "name": "memory_create_entities"
  }
}
```

---

#### 3. tool_execute - 执行 CLI 工具

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

### 完整对话流程示例

#### 场景：记录项目信息并查询

**步骤1 - 搜索可用工具：**
```
用户：有哪些可用的记忆工具？

AI调用：tool_search(query="memory")

返回：
- memory_create_entities: 创建实体
- memory_search_nodes: 搜索节点
- memory_read_graph: 读取图谱
...
```

**步骤2 - 创建实体：**
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
