# CLAUDE.local.md - Windows 本地环境规则

> 📎 **定位**: 本文档为 Windows 平台的本地覆盖规则，不入 Git 仓库
>
> ⚠️ **免责声明**: 此文件仅适用于 Windows 开发环境，Mac/Linux 用户请自行维护各自的 `CLAUDE.local.md`

***

## 封装要求

封装API的时候你要学会吝啬,尽可能少暴露公开的接口.
但是测试时候需要测试到内部接口,可以内部的是 internal 类.
这样主工程调用的时候就非常少API,显得更优美.



## 🔴 平台专属操作禁令

### PowerShell 相关

1. **❌ 禁止使用 PowerShell `Set-Content` 修改 C# 文件**
   - 错误编号: CS1022
   - 原因: 可能导致文件损坏
   - 正确: 使用 IDE 的 `SearchReplace` 工具修改文件内容
2. **❌ 禁止使用 PowerShell 交互式命令**
   - 禁止: `Out-Host -Paging`
   - 推荐: 使用 `| Select-Object -First N` 替代分页

### Git 操作（PowerShell 环境）

- 设置无分页环境变量: `$env:GIT_PAGER='cat'`
- 所有 git 命令必须使用 `--no-pager` 参数
- **⛔ 禁止执行 `git push`**：LLM 只能执行 `git commit`，推送操作必须由用户手动完成

### 文件"安全删除"（PowerShell 命令）

```
概念: 移动到 .x/ 目录代替删除
格式: .x/{原文件名}.{原后缀}.{时间戳}.del
```

```powershell
# 创建安全删除目录（如果不存在）
if (-not (Test-Path ".x")) { New-Item -ItemType Directory -Path ".x" }

# 移动文件并标记时间戳
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
Move-Item "oldfile.cs" ".x\oldfile.cs.$ts.del"
```

***

## ⚠️ Windows 命令行环境

### 路径格式
- 使用反斜杠 `\` 作为路径分隔符
  - 正确: `C:\Users\Name\Documents`
  - 错误: `/home/user/project`

### 命令分隔
- **禁止使用 `&&`** 连接命令
- 首选: 分步说明，每个命令单独一行
- PowerShell: 使用分号 `;` 连接
- CMD: 可使用单个 `&`（但忽略前序失败）

### 原生工具优先
- 优先使用 Windows 原生命令（`dir`, `findstr`）
- 或 PowerShell cmdlet（`Get-ChildItem`, `Select-String`）
- 避免依赖 Unix 工具（`grep`, `sed`, `awk`），除非明确要求 WSL

### UTF-8 编码配置
```powershell
[Console]::OutputEncoding = [System.Text.Encoding]UTF8
chcp 65001
```
