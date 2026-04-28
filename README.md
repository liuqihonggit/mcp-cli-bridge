# @jingjingbox/mcp-cli-bridge

MCP-CLI 桥接服务器 - 通过 MCP 协议调用 CLI 工具。

## 安装

```bash
npm install -g @jingjingbox/mcp-cli-bridge
```

## 配置

### npx

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

## 环境变量

| 变量 | 描述 | 必填 |
|------|------|------|
| `MCP_MEMORY_PATH` | MemoryCli 知识图谱数据存储目录 | **是** |

配置目录下会自动创建 `memory.jsonl` 和 `memory_relations.jsonl`。

## License

MIT
