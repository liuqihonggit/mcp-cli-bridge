# AstCli - AST 代码分析工具

## 概述

AstCli 提供 C# 代码的 AST（抽象语法树）分析能力，支持符号查询、引用查找、重命名替换、项目概览、文件上下文、编译诊断和符号大纲。

## 命令列表

### ast_symbol_query

在 C# 项目中按名称查询符号。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `symbol_query` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 符号名称（支持 `*` 通配符） |
| scope | string | 否 | 搜索范围：`project`（默认）或 `file`（仅顶层目录） |

### ast_reference_find

查找符号在项目中的所有引用。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `reference_find` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 要查找引用的符号名称 |

### ast_symbol_rename

跨文件重命名符号。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `symbol_rename` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 当前符号名称 |
| newName | string | 是 | 新符号名称 |

### ast_symbol_replace

跨文件替换符号名称。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `symbol_replace` |
| projectPath | string | 是 | 项目根目录路径 |
| symbolName | string | 是 | 旧符号名称 |
| newName | string | 是 | 新符号名称 |

### ast_symbol_info

获取文件指定位置的符号信息。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `symbol_info` |
| filePath | string | 是 | 源文件路径 |
| lineNumber | integer | 是 | 行号（0 起始） |
| columnNumber | integer | 是 | 列号（0 起始） |

### ast_workspace_overview

获取项目结构概览：文件统计、命名空间树、csproj 引用、目录角色推断、入口点识别。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `workspace_overview` |
| projectPath | string | 是 | 项目根目录路径 |

### ast_file_context

分析文件上下文：using 语句、同项目符号引用、同命名空间公开符号、反向依赖。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `file_context` |
| projectPath | string | 是 | 项目根目录路径 |
| filePath | string | 是 | 目标文件路径 |

### ast_diagnostics

获取语法诊断信息（仅语法层，不含语义错误）。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `diagnostics` |
| projectPath | string | 是 | 项目根目录路径 |
| filePath | string | 否 | 指定文件（不传则扫描整个项目） |

### ast_symbol_outline

获取文件符号大纲：类型声明、成员列表、行号范围、访问修饰符。

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| command | string | 是 | `symbol_outline` |
| filePath | string | 是 | 目标文件路径 |

## 特性

- **无状态**：每次调用即时解析，不缓存 AST
- **AOT 兼容**：支持 NativeAOT 编译
- **文件锁安全**：使用 FileLockService 确保并发安全
- **自动排除**：跳过 bin/obj/.git/node_modules 目录
- **9 个命令**：5 个符号操作 + 1 个引用操作 + 1 个工作区 + 1 个文件上下文 + 1 个诊断
