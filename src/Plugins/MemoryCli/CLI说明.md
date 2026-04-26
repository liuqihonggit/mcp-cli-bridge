# MemoryCli 说明文档

MemoryCli 是一个知识图谱管理工具，用于创建、查询和管理实体及其关系。

## 工具列表

### 1. memory_create_entities

创建多个新实体到知识图谱中。

**描述**: Create multiple new entities in the knowledge graph

**参数**:
| 参数名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| command | string | 是 | 固定值: `create_entities` |
| entities | array | 是 | 要创建的实体列表 |

**实体对象结构**:
| 字段名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| name | string | 是 | 实体名称 |
| entityType | string | 是 | 实体类型 |
| observations | array | 否 | 观察记录列表 |

**示例**:
```json
{
  "command": "create_entities",
  "entities": [
    {
      "name": "用户A",
      "entityType": "Person",
      "observations": ["是一名开发者", "喜欢编程"]
    }
  ]
}
```

---

### 2. memory_create_relations

在实体之间创建关系。

**描述**: Create relations between entities

**参数**:
| 参数名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| command | string | 是 | 固定值: `create_relations` |
| relations | array | 是 | 要创建的关系列表 |

**关系对象结构**:
| 字段名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| from | string | 是 | 起始实体名称 |
| to | string | 是 | 目标实体名称 |
| relationType | string | 是 | 关系类型 |

**示例**:
```json
{
  "command": "create_relations",
  "relations": [
    {
      "from": "用户A",
      "to": "项目B",
      "relationType": "works_on"
    }
  ]
}
```

---

### 3. memory_read_graph

读取整个知识图谱。

**描述**: Read the entire knowledge graph

**参数**:
| 参数名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| command | string | 是 | 固定值: `read_graph` |

**示例**:
```json
{
  "command": "read_graph"
}
```

---

### 4. memory_search_nodes

在知识图谱中搜索节点。

**描述**: Search for nodes in the knowledge graph

**参数**:
| 参数名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| command | string | 是 | 固定值: `search_nodes` |
| query | string | 是 | 搜索关键词 |

**示例**:
```json
{
  "command": "search_nodes",
  "query": "用户"
}
```

---

### 5. memory_add_observations

向现有实体添加观察记录。

**描述**: Add observations to existing entities

**参数**:
| 参数名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| command | string | 是 | 固定值: `add_observations` |
| name | string | 是 | 实体名称 |
| observations | array | 是 | 要添加的观察记录 |

**示例**:
```json
{
  "command": "add_observations",
  "name": "用户A",
  "observations": ["最近学习了Rust"]
}
```

---

### 6. memory_delete_entities

从图谱中删除实体及其相关关系。

**描述**: Delete entities from the graph

**参数**:
| 参数名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| command | string | 是 | 固定值: `delete_entities` |
| names | array | 是 | 要删除的实体名称列表 |

**示例**:
```json
{
  "command": "delete_entities",
  "names": ["用户A", "项目B"]
}
```

---

### 7. memory_open_nodes

按名称获取特定节点。

**描述**: Get specific nodes by name

**参数**:
| 参数名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| command | string | 是 | 固定值: `open_nodes` |
| names | array | 是 | 要获取的实体名称列表 |

**示例**:
```json
{
  "command": "open_nodes",
  "names": ["用户A", "用户B"]
}
```

---

## 响应格式

所有工具返回统一的 `OperationResult` 格式：

```json
{
  "success": true,
  "message": "操作结果描述",
  "data": { ... },
  "exitCode": 0,
  "executionTimeMs": 0
}
```

## 错误处理

当操作失败时，`success` 为 `false`，`message` 包含错误信息：

```json
{
  "success": false,
  "message": "错误描述",
  "data": {},
  "exitCode": 1
}
```
