# AstCli - AST 代码分析工具

## 概述

AstCli 提供 C# 代码的 AST（抽象语法树）分析能力，支持符号查询、引用查找、重命名和替换操作。

## 命令列表

### ast_query_symbol

在 C# 项目中按名称查询符号。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `query_symbol` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 符号名称（支持 `*` 通配符） |
| scope | string | 否 | 搜索范围：`project`（默认）或 `file`（仅顶层目录） |

### ast_find_references

查找符号在项目中的所有引用。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `find_references` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 要查找引用的符号名称 |

### ast_rename_symbol

跨文件重命名符号。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `rename_symbol` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 当前符号名称 |
| newName | string | 是 | 新符号名称 |

### ast_replace_symbol

跨文件替换符号名称。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `replace_symbol` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 旧符号名称 |
| newName | string | 是 | 新符号名称 |

### ast_get_symbol_info

获取文件指定位置的符号信息。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `get_symbol_info` |
| filePath | string | 是 | 源文件路径 |
| lineNumber | integer | 是 | 行号（0 起始） |
| columnNumber | integer | 是 | 列号（0 起始） |

## 特性

- **无状态**：每次调用即时解析，不缓存 AST
- **AOT 兼容**：支持 NativeAOT 编译
- **文件锁安全**：使用 FileLockService 确保并发安全
- **自动排除**：跳过 bin/obj/.git/node_modules 目录
