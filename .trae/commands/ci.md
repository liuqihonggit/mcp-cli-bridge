---
description: 发布新版本到 npm（完整流程：验证→打版本→commit+tag→输出push命令）
argument-hint: patch | minor | major (默认: patch)
tools: RunCommand, Read, SearchReplace, Write, Grep, Glob
---

# /ci - 发布新版本

> **版本类型**: ${1:-patch}
> **当前本地版本**: v!\`cat package.json | findstr version | findstr /R "[0-9]"`\`

## ⚠️ 强制规则（执行前必读）

- ❌ **禁止执行 `git push`** — 只输出推送命令，由用户手动执行
- ❌ **禁止执行 `buildAndNpm.ps1`** — CI/CD 专属脚本
- ❌ **禁止手动 `npm publish`** — 由 CI/CD 自动完成
- ✅ 必须走完完整验证流水线后才能 release
- ✅ Git 必须在干净状态下开始

---

## Step 1: 前置检查

### 1.1 检查 Git 工作区是否干净

运行以下命令确认无未提交更改：

```
git --no-pager status --porcelain
```

如果输出非空，**立即停止**，告知用户先提交或暂存更改。

### 1.2 确认当前版本信息

- 本地 package.json 版本: 从上方动态获取
- npm registry 最新版: 运行 `npm view @jingjingbox/mcp-cli-bridge version 2>$null`
- 预计新版本: 根据版本类型计算

---

## Step 2: 完整验证流水线（必须全部通过）

按顺序执行以下每一步，**任何一步失败都停止**：

### 2.1 编译全部项目

```bash
dotnet build McpHost.slnx -c Release
```

✅ 零错误零警告才可继续。

### 2.2 运行单元测试

```bash
dotnet test McpHost.slnx -c Release
```

✅ 全部测试通过才可继续。

### 2.3 运行 E2E 测试

```bash
dotnet run --project tests\E2E\MyMemoryServer.E2E.csproj -c Release
```

✅ 协议边界测试通过才可继续。

### 2.4 AOT 发布构建

```bash
.\build.ps1
```

✅ publish/ 目录产物正确生成才可继续。

> 如果修改过 `.github/workflows/build.yml`，在 Step 2 之前还需要先执行 [CI-CD发布流程.md](./AI交互文档/CI-CD发布流程.md) 中的本地验证脚本。

---

## Step 3: 执行 Release

验证全部通过后，执行版本发布脚本：

```bash
.\release.ps1 -VersionBump ${1:-patch}
```

该脚本会自动：
1. 读取并递增版本号（package.json + Directory.Build.props）
2. git add + git commit（消息: `release: v{x.y.z}`）
3. git tag `v{x.y.z}`
4. 输出推送命令（**不执行 push**）

---

## Step 4: 输出 Push 命令（用户手动执行）

Release 脚本执行成功后，向用户展示以下推送命令：

```powershell
# === 推送到 Gitee (origin) ===
git push origin main && git push origin v{新版本号}

# === 推送到 GitHub (github) ← 触发 CI/CD 自动发布 npm ===
git push github main && git push github v{新版本号}
```

---

## Step 5: 后续监控

推送后告知用户：

```
📡 CI/CD 监控地址:
   https://github.com/liuqihonggit/mcp-cli-bridge/actions

🔄 CI/CD 将自动执行:
   ① AOT 编译 + 单元测试 + E2E 测试
   ② npm publish（发布到 npm registry）
   ③ GitHub Release（上传 exe 文件）

📦 CI/CD 成功后安装新版本:
   npm install -g @jingjingbox/mcp-cli-bridge@latest
```

---

## 错误处理

| 失败阶段 | 处理方式 |
|---------|---------|
| Git 工作区不干净 | 停止，让用户先提交 |
| 编译失败 | 修复代码后从 Step 2.1 重来 |
| 测试失败 | 修复后从对应测试步骤重来 |
| build.ps1 失败 | 检查 NativeAOT 兼容性 |
| release.ps1 失败 | 检查 tag 冲突或版本号问题 |
