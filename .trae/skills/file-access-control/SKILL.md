---
name: "file-access-control"
description: "Enforces file access control using .csx C# script locks with 5s acquisition timeout and 30s auto-expiry. Invoke when agent needs to read/write files to ensure proper locking mechanism."
---

# File Access Control

This skill enforces mandatory file locking before any file access operations to prevent race conditions and ensure data integrity.

## Workflow

```
访问文件前
    ↓
检查 .csx 锁脚本是否存在?
    ↓ 否
创建锁脚本 (子智能体)
    ↓
调用 .csx 脚本获取文件锁
    ↓
5秒内获取成功?
    ↓ 否                    ↓ 是
委派子智能体做其他事情    执行文件操作
                          ↓
                          释放锁
```

## 命名规范

### 锁脚本名称

| 常量 | 值 | 说明 |
|------|-----|------|
| `LockScript.FileName` | `FileAccessGuard.csx` | 文件访问守卫脚本 |
| `LockScript.Directory` | `.trae/skills/file-access-control/` | 脚本存放目录 |

## Rules

### 1. 锁脚本检查与创建

**访问任何文件前，必须先检查锁脚本是否存在：**

```csharp
// 使用 nameof 和 typeof 避免硬编码
public static class LockScript
{
    public const string FileName = nameof(FileAccessGuard) + ".csx";
    public const string Directory = ".trae/skills/file-access-control/";
    public static readonly string FullPath = Path.Combine(Directory, FileName);
}

// 检查脚本是否存在
if (!File.Exists(LockScript.FullPath))
{
    // 委派子智能体创建锁脚本 FileAccessGuard.csx
    // 不要自己创建，让子智能体来做
}
```

### 2. 调用锁脚本获取锁

```bash
# 使用 dotnet-script 运行 .csx 脚本
dotnet script .trae\skills\file-access-control\FileAccessGuard.csx -- "C:\path\to\file.txt" acquire
```

### 3. 锁脚本标准接口

锁脚本必须支持以下命令：

| 命令 | 功能 | 返回值 |
|------|------|--------|
| `acquire <filepath>` | 获取文件锁 | `SUCCESS` / `TIMEOUT` / `ERROR` |
| `release <filepath>` | 释放文件锁 | `SUCCESS` / `ERROR` |
| `status <filepath>` | 检查锁状态 | `LOCKED:<pid>` / `FREE` / `EXPIRED` |

## 锁脚本实现 (FileAccessGuard.csx)

```csharp
#!/usr/bin/env dotnet-script
#r "System.IO.FileSystem"

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

// 配置
const int ACQUISITION_TIMEOUT_SECONDS = 5;  // 5秒抢不到就放弃
const int LOCK_EXPIRY_SECONDS = 30;         // 锁30秒自动过期
const int RETRY_INTERVAL_MS = 100;          // 重试间隔100毫秒

// 命令行参数解析
if (Args.Count < 2)
{
    Console.WriteLine("ERROR: Usage: dotnet script FileLock.csx -- <command> <filepath>");
    Console.WriteLine("Commands: acquire, release, status");
    Environment.Exit(1);
}

string command = Args[0].ToLower();
string targetFilePath = Args[1];
string lockFilePath = $"{targetFilePath}.scx.lock";

switch (command)
{
    case "acquire":
        Environment.Exit(await AcquireLockAsync() ? 0 : 1);
        break;
    case "release":
        Environment.Exit(ReleaseLock() ? 0 : 1);
        break;
    case "status":
        Console.WriteLine(GetLockStatus());
        Environment.Exit(0);
        break;
    default:
        Console.WriteLine($"ERROR: Unknown command '{command}'");
        Environment.Exit(1);
        break;
}

// 获取锁
async Task<bool> AcquireLockAsync()
{
    var startTime = DateTime.UtcNow;

    while ((DateTime.UtcNow - startTime).TotalSeconds < ACQUISITION_TIMEOUT_SECONDS)
    {
        // 清理过期锁
        await TryRemoveExpiredLockAsync();

        try
        {
            // 尝试独占创建锁文件
            using var fs = new FileStream(
                lockFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);

            using var writer = new StreamWriter(fs);
            await writer.WriteLineAsync($"PID:{Environment.ProcessId}");
            await writer.WriteLineAsync($"Time:{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            await writer.WriteLineAsync($"ExpirySeconds:{LOCK_EXPIRY_SECONDS}");
            await writer.FlushAsync();

            Console.WriteLine("SUCCESS");
            return true;
        }
        catch (IOException)
        {
            // 锁被占用，等待重试
            await Task.Delay(RETRY_INTERVAL_MS);
        }
    }

    // 5秒超时
    Console.WriteLine("TIMEOUT");
    return false;
}

// 释放锁
bool ReleaseLock()
{
    try
    {
        if (File.Exists(lockFilePath))
        {
            File.Delete(lockFilePath);
            Console.WriteLine("SUCCESS");
            return true;
        }
        Console.WriteLine("SUCCESS: Lock not found");
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: {ex.Message}");
        return false;
    }
}

// 获取锁状态
string GetLockStatus()
{
    if (!File.Exists(lockFilePath))
        return "FREE";

    try
    {
        var lines = File.ReadAllLines(lockFilePath);
        DateTime? lockTime = null;
        double expirySeconds = LOCK_EXPIRY_SECONDS;
        string? pid = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("PID:"))
                pid = line[4..].Trim();
            if (line.StartsWith("Time:") && DateTime.TryParse(line[5..].Trim(), out var lt))
                lockTime = lt.ToUniversalTime();
            if (line.StartsWith("ExpirySeconds:") && double.TryParse(line[14..].Trim(), out var exp))
                expirySeconds = exp;
        }

        // 检查是否过期
        if (lockTime.HasValue && (DateTime.UtcNow - lockTime.Value).TotalSeconds > expirySeconds)
            return "EXPIRED";

        return pid != null ? $"LOCKED:{pid}" : "LOCKED:UNKNOWN";
    }
    catch
    {
        return "ERROR";
    }
}

// 清理过期锁
async Task TryRemoveExpiredLockAsync()
{
    if (!File.Exists(lockFilePath))
        return;

    try
    {
        var lines = await File.ReadAllLinesAsync(lockFilePath);
        DateTime? lockTime = null;
        double expirySeconds = LOCK_EXPIRY_SECONDS;

        foreach (var line in lines)
        {
            if (line.StartsWith("Time:") && DateTime.TryParse(line[5..].Trim(), out var lt))
                lockTime = lt.ToUniversalTime();
            if (line.StartsWith("ExpirySeconds:") && double.TryParse(line[14..].Trim(), out var exp))
                expirySeconds = exp;
        }

        if (lockTime.HasValue && (DateTime.UtcNow - lockTime.Value).TotalSeconds > expirySeconds)
        {
            File.Delete(lockFilePath);
        }
    }
    catch { /* 忽略清理错误 */ }
}
```

## 智能体使用流程

### 步骤1: 检查并创建锁脚本

```csharp
// 智能体代码
string skillDir = @".trae\skills\file-access-control";
string lockScriptPath = Path.Combine(skillDir, "FileLock.csx");

// 检查脚本是否存在
if (!File.Exists(lockScriptPath))
{
    // 委派子智能体创建脚本
    // 子智能体任务: 创建 FileLock.csx 脚本
}
```

### 步骤2: 调用脚本获取锁

```bash
# 在 PowerShell/CMD 中调用
dotnet script .trae\skills\file-access-control\FileAccessGuard.csx -- "C:\data\file.txt" acquire

# 检查返回值
if ($LASTEXITCODE -eq 0) {
    # 获取锁成功，执行文件操作
} else {
    # 获取锁失败（超时），委派子智能体做其他事情
}
```

### 步骤3: 执行文件操作并释放锁

```bash
# 执行文件操作后，释放锁
dotnet script .trae\skills\file-access-control\FileAccessGuard.csx -- "C:\data\file.txt" release
```

## 完整示例 (智能体实现)

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

public class FileAccessAgent
{
    private readonly string _lockScriptPath;

    public FileAccessAgent()
    {
        // 使用 nameof 避免硬编码
        _lockScriptPath = $@".trae\skills\file-access-control\{nameof(FileAccessGuard)}.csx";
    }

    /// <summary>
    /// 访问文件（带锁控制）
    /// </summary>
    public async Task<string?> AccessFileAsync(string filePath, Func<string, Task<string?>> operation)
    {
        // 1. 确保锁脚本存在
        if (!await EnsureLockScriptExistsAsync())
        {
            throw new InvalidOperationException("无法创建锁脚本");
        }

        // 2. 尝试获取锁
        if (!await TryAcquireLockAsync(filePath))
        {
            // 5秒没抢到锁，委派子智能体做其他事情
            Console.WriteLine($"文件被占用: {filePath}，委派子智能体处理其他任务...");
            // TODO: 启动子智能体处理其他任务
            return null;
        }

        try
        {
            // 3. 执行文件操作
            return await operation(filePath);
        }
        finally
        {
            // 4. 释放锁
            await ReleaseLockAsync(filePath);
        }
    }

    private async Task<bool> EnsureLockScriptExistsAsync()
    {
        if (File.Exists(_lockScriptPath))
            return true;

        // 委派子智能体创建脚本
        // 这里应该调用子智能体，而不是自己创建
        // 简化示例：直接创建
        var scriptDir = Path.GetDirectoryName(_lockScriptPath);
        if (!Directory.Exists(scriptDir))
            Directory.CreateDirectory(scriptDir);

        // 写入脚本内容（实际应该由子智能体完成）
        // ...
        return true;
    }

    private async Task<bool> TryAcquireLockAsync(string filePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"script \"{_lockScriptPath}\" -- \"{filePath}\" acquire",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi);
        await process.WaitForExitAsync();

        return process.ExitCode == 0;
    }

    private async Task ReleaseLockAsync(string filePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"script \"{_lockScriptPath}\" -- \"{filePath}\" release",
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi);
        await process.WaitForExitAsync();
    }
}
```

## 超时策略

| 阶段 | 超时时间 | 行为 |
|------|----------|------|
| 锁获取 | 5秒 | 抢不到就放弃，委派子智能体做其他事情 |
| 锁有效期 | 30秒 | 自动过期，防止死锁 |
| 重试间隔 | 100毫秒 | 轮询检查锁状态 |

## 锁文件格式 (.scx.lock)

```
PID:12345
Time:2026-04-22 10:30:00
ExpirySeconds:30
```

## 最佳实践

1. **先检查脚本存在性** - 访问文件前先确认锁脚本存在
2. **不存在则委派创建** - 使用子智能体创建锁脚本，不要自己创建
3. **5秒非阻塞** - 抢不到锁立即返回，不要无限等待
4. **任务委派** - 获取锁失败后，立即启动子智能体处理其他任务
5. **确保释放锁** - 使用 `try-finally` 确保锁一定会被释放
