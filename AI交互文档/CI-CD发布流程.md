# CI/CD 发布流程指南

> 📎 **定位**: 修改 `build.yml` 后的本地验证方法 + CI 排障手册
>
> ⚠️ **触发场景**:
> - 修改 `.github/workflows/build.yml` 或 `publish-npm.yml` 后必须验证
> - CI/CD 失败时排查问题
> - 发布新版本前确认流程正确

***

## 1️⃣ 本地验证：模拟 CI 完整流程

> **为什么需要？** CI 环境与本地存在差异（路径、缓存、nuget源等），推送后才发现问题浪费大量时间。

### 验证脚本（复制到终端执行）

```powershell
# === Step 0: 彻底清理（模拟 CI 的 Clean 步骤）===
if (Test-Path "nuget") { Remove-Item -Path "nuget" -Recurse -Force }
if (Test-Path "publish") { Remove-Item -Path "publish" -Recurse -Force }

# === Step 1: 模拟 CI 新增的预编译步骤（按 build.yml 顺序执行）===
# 关键：创建空 nuget/ 目录！否则 restore 会报 NU1301 错误
New-Item -ItemType Directory -Path "nuget" -Force | Out-Null

# Restore lib/ 项目（使用 nuget.config）
Get-ChildItem -Path "lib" -Recurse -Filter "*.csproj" -File | ForEach-Object {
    $name = $_.BaseName
    if ($name -like "*.Tests") { return }
    Write-Host "  Restoring $name..." -ForegroundColor Gray
    dotnet restore $_.FullName --configfile nuget.config
}

# Build lib/ 项目
Get-ChildItem -Path "lib" -Recurse -Filter "*.csproj" -File | ForEach-Object {
    $name = $_.BaseName
    if ($name -like "*.Tests") { return }
    Write-Host "  Building $name..." -ForegroundColor Gray
    dotnet build $_.FullName -c Release --no-restore
}

# === Step 2: 验证关键 dll 是否生成 ===
Write-Host "`n[Verify] Checking dll files..." -ForegroundColor Yellow
$libDlls = @(
    "lib\AsyncFileLock\src\AsyncFileLock\bin\Release\net10.0\AsyncFileLock.dll",
    "lib\McpProtocol\src\McpProtocol.Contracts\bin\Release\net10.0\McpProtocol.Contracts.dll",
    "lib\McpProtocol\src\McpProtocol\bin\Release\net10.0\McpProtocol.dll"
)
foreach ($dll in $libDlls) {
    if (Test-Path $dll) { Write-Host "  OK: $dll" -ForegroundColor Green }
    else { Write-Host "  FAIL: $dll" -ForegroundColor Red; exit 1 }
}

# === Step 3: 执行完整 AOT Build ===
Write-Host "`n[Build] Running build.ps1..." -ForegroundColor Cyan
.\build.ps1
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "`n✅ CI 模拟通过！可以安全推送。" -ForegroundColor Green
```

## 2️⃣ CI vs 本地差异速查表

| 差异点 | 本地环境 | CI 环境 | 解决方案 |
| ------ | -------- | ------- | -------- |
| **nuget/ 目录** | 已存在（有历史缓存） | Clean 步骤删除后不存在 | CI 中必须先 `New-Item nuget` |
| **NuGet 缓存** | `~/.nuget/packages` 有缓存 | 全新环境无缓存 | lib 项目需显式 restore + build |
| **nuget.config** | 本地 restore 自动找到 | 必须显式指定 `--configfile` | CI 步骤中传入 config |
| **路径分隔符** | PowerShell 自动兼容 | GitHub Actions 用 `windows-latest` | 统一用 `\` 或 `/` |

## 3️⃣ 经验教训案例库

### 案例 1: v3.0.12 - McpProtocol.Contracts.dll 缺失

**错误信息**:
```
The file '...\McpProtocol.Contracts.dll' to be packed was not found on disk.
```

**根因链**:
```
CI Clean 删除 nuget/
  → build.ps1 内 dotnet pack 尝试 pack McpProtocol.Contracts
    → pack 需要 dll 存在于 bin/Release/
      → 但 nuget.config 指向的 local 源 nuget/ 不存在
        → restore 报 NU1301: 本地源不存在
          → dll 未生成
            → pack 失败
```

**修复**: 在 build.yml 的 AOT Build 步骤前新增：
```yaml
- name: Restore and build local NuGet packages (lib/)
  run: |
    New-Item -ItemType Directory -Path "nuget" -Force | Out-Null
    # ... restore + build lib/ projects
```

### 案例 2: NU1301 - 本地源不存在

**错误信息**:
```
error NU1301: 本地源"G:\...\nuget"不存在。
```

**原因**: `dotnet restore` 会自动向上查找 `nuget.config`，即使不指定 `--configfile`

**修复**: Restore 前先创建空目录：
```powershell
New-Item -ItemType Directory -Path "nuget" -Force | Out-Null
```

## 4️⃣ CI/CD 工作流架构

### 触发条件

| 工作流 | 触发条件 | 用途 |
| ------ | -------- | ---- |
| [build.yml](../.github/workflows/build.yml) | push to main/master/develop, PR to main | 编译 + 测试 + 打包 artifacts |
| [publish-npm.yml](../.github/workflows/publish-npm.yml) | push tag `v*` | 下载 artifacts → npm publish → GitHub Release |

### 执行顺序

```
push tag v3.0.13
  ├── build.yml (#18)
  │   ├── Checkout
  │   ├── Setup .NET 10
  │   ├── Setup Node.js 22
  │   ├── Clean (删除 nuget/ publish/)
  │   ├── Restore & Build lib/ ← 新增修复步骤
  │   ├── AOT Build (buildAndNpm.ps1)
  │   │   └── build.ps1 (pack lib → restore src → AOT publish)
  │   ├── Unit Tests
  │   ├── E2E Tests
  │   └── Upload artifact (publish/)
  │
  └── publish-npm.yml (#6)
      ├── Download artifact (publish/)
      ├── Inject version from tag
      ├── npm publish
      └── GitHub Release (exe files)
```

## 5️⃣ 排障检查清单

CI 失败时按此顺序排查：

- [ ] **Clean 步骤是否删除了 nuget/** → 检查 build.yml 中是否有预编译步骤
- [ ] **lib 项目是否成功 restore** → 查看 NU1301 错误
- [ ] **dll 文件是否生成** → 检查 pack 步骤前的 build 输出
- [ ] **AOT 编译是否通过** → 检查 NativeAOT 兼容性（动态类型、反射 emit）
- [ ] **测试是否通过** → Unit Tests + E2E Tests
- [ ] **npm publish 是否成功** → 检查 NPM_TOKEN secret、版本号冲突
- [ ] **Node.js 版本警告** → actions/checkout@v4 等 Node.js 20 即将弃用

## 6️⃣ 发布版本完整流程

```powershell
# 1. 确保代码已提交且测试通过
dotnet build McpHost.slnx -c Release
dotnet test McpHost.slnx -c Release
.\build.ps1

# 2. 如果修改了 build.yml，先执行第1节的本地验证脚本 ✅

# 3. 发布新版本
.\release.ps1 -VersionBump patch   # 或 minor/major

# 4. 手动推送（AI禁止执行 git push）
git push github main && git push github v{新版本号}

# 5. 监控 CI/CD
# https://github.com/liuqihonggit/mcp-cli-bridge/actions
```
