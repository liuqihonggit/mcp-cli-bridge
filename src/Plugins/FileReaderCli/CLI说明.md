# FileReaderCli 说明文档

FileReaderCli 是一个文件读取工具，用于高效读取文件内容。

## 工具列表

### 1. file_reader_read_head

读取文件的前 N 行内容。

**描述**: Read the first N lines from a file

**参数**:
| 参数名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| command | string | 是 | 固定值: `read_head` |
| filePath | string | 是 | 要读取的文件路径 |
| lineCount | integer | 否 | 要读取的行数（默认: 10） |

**示例**:
```json
{
  "command": "read_head",
  "filePath": "C:\\path\\to\\file.txt",
  "lineCount": 5
}
```

**响应示例**:
```json
{
  "success": true,
  "message": "Read 5 lines from file.txt (total: 100 lines)",
  "data": {
    "filePath": "C:\\path\\to\\file.txt",
    "lines": ["Line 1", "Line 2", "Line 3", "Line 4", "Line 5"],
    "totalLines": 100,
    "requestedLines": 5
  },
  "exitCode": 0
}
```

---

### 2. file_reader_read_tail

读取文件的后 N 行内容。

**描述**: Read the last N lines from a file

**参数**:
| 参数名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| command | string | 是 | 固定值: `read_tail` |
| filePath | string | 是 | 要读取的文件路径 |
| lineCount | integer | 否 | 要读取的行数（默认: 10） |

**示例**:
```json
{
  "command": "read_tail",
  "filePath": "C:\\path\\to\\file.txt",
  "lineCount": 3
}
```

**响应示例**:
```json
{
  "success": true,
  "message": "Read last 3 lines from file.txt (total: 100 lines)",
  "data": {
    "filePath": "C:\\path\\to\\file.txt",
    "lines": ["Line 98", "Line 99", "Line 100"],
    "totalLines": 100,
    "requestedLines": 3
  },
  "exitCode": 0
}
```

---

## 响应格式

所有工具返回统一的 `OperationResult` 格式：

```json
{
  "success": true,
  "message": "操作结果描述",
  "data": {
    "filePath": "文件路径",
    "lines": ["行1", "行2", ...],
    "totalLines": 总行数,
    "requestedLines": 请求行数
  },
  "exitCode": 0,
  "executionTimeMs": 0
}
```

## 错误处理

当文件不存在或读取失败时：

```json
{
  "success": false,
  "message": "File not found: C:\\path\\to\\file.txt",
  "data": {},
  "exitCode": 1
}
```

## 使用场景

1. **日志分析**: 快速查看日志文件的开头或结尾
2. **配置检查**: 读取配置文件的前几行
3. **数据预览**: 预览大型数据文件的结构
4. **错误排查**: 查看错误日志的最新记录
