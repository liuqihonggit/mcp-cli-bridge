# E2E测试失败问题报告

**日期**: 2026-04-30  
**状态**: ✅ 已解决  
**严重程度**: 高

---

## 问题概述

E2E测试失败，主要表现为两个错误：
1. MCP协议版本错误：服务器返回 "2.0" 而不是期望的 "2024-11-05"
2. tools/list 方法崩溃：抛出 `Operation is not valid due to the current state of the object.` 异常

---

## 根本原因分析

### 问题1: 协议版本错误

**原因**: `McpServer.HandleInitialize()` 方法返回的是 JSON-RPC 协议版本 `2.0`，而不是 MCP 协议版本 `2024-11-05`

**问题代码** (`lib/McpProtocol/src/McpProtocol/McpServer.cs`):
```csharp
private InitializeResult HandleInitialize()
{
    return new InitializeResult
    {
        ProtocolVersion = JsonRpc.ProtocolVersion,  // 错误: 返回 "2.0" (JSON-RPC版本)
        ...
    };
}
```

### 问题2: 项目引用错误

**原因**: McpHost 项目引用的是 NuGet 包 `McpProtocol 1.0.1`，而不是本地项目引用。导致修改本地代码后不生效。

**问题代码** (`src/McpHost/McpHost.csproj`):
```xml
<ItemGroup>
  <PackageReference Include="McpProtocol" Version="1.0.1" />
</ItemGroup>
```

### 问题3: NuGet 缓存导致修改不生效

**原因**: 修改 `lib/McpProtocol` 代码后，NuGet 缓存中仍然保留旧版本的包，导致构建时使用的是旧代码。

**解决方案**: 创建自动清理缓存的脚本，每次构建前自动打包并清理缓存。

---

## 解决方案

### 修复1: 添加 MCP 协议版本常量

**文件**: `lib/McpProtocol/src/McpProtocol.Contracts/Constants/JsonRpcConstants.cs`

```csharp
public static class JsonRpc
{
    public const string ProtocolVersion = "2.0";
    public const string ContentLengthPrefix = "Content-Length: ";
}

public static class McpProtocolVersion
{
    public const string Current = "2024-11-05";
}
```

### 修复2: 使用正确的协议版本

**文件**: `lib/McpProtocol/src/McpProtocol/McpServer.cs`

```csharp
private InitializeResult HandleInitialize()
{
    return new InitializeResult
    {
        ProtocolVersion = McpProtocolVersion.Current,  // 修复: 使用 MCP 协议版本
        Capabilities = new ServerCapabilities
        {
            Tools = new ToolsCapability { ListChanged = false }
        },
        ServerInfo = new Implementation
        {
            Name = _serverName,
            Version = _serverVersion
        }
    };
}
```

### 修复3: 使用本地 NuGet 包

**文件**: `src/McpHost/McpHost.csproj`

```xml
<ItemGroup>
  <PackageReference Include="McpProtocol" Version="1.0.2" />
</ItemGroup>
```

**文件**: `src/Common/Common.csproj`

```xml
<ItemGroup>
  <PackageReference Include="McpProtocol" Version="1.0.2" />
</ItemGroup>
```

### 修复4: 创建自动清理缓存脚本

**文件**: `scripts/clear-nuget-cache.ps1`

```powershell
#!/usr/bin/env powershell
$packagesToClean = @("McpProtocol", "McpProtocol.Contracts")
$nugetCachePath = "$env:USERPROFILE\.nuget\packages"

foreach ($package in $packagesToClean) {
    $packageCachePath = Join-Path $nugetCachePath $package.ToLowerInvariant()
    if (Test-Path $packageCachePath) {
        Remove-Item -Path $packageCachePath -Recurse -Force
    }
}
dotnet nuget locals http-cache --clear
```

### 修复5: 修改 build.ps1 集成缓存清理

**文件**: `build.ps1`

构建流程：
1. 打包 `lib/McpProtocol` 到本地 `nuget/` 目录
2. 清理 NuGet 缓存
3. 构建 AOT 项目

**文件**: `nuget.config`

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="nuget" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

---

## 修改的文件汇总

| 文件 | 修改内容 |
|------|----------|
| `lib/McpProtocol/src/McpProtocol.Contracts/Constants/JsonRpcConstants.cs` | 添加 `McpProtocolVersion.Current = "2024-11-05"` |
| `lib/McpProtocol/src/McpProtocol/McpServer.cs` | 使用 `McpProtocolVersion.Current` 替代 `JsonRpc.ProtocolVersion` |
| `lib/McpProtocol/src/McpProtocol/McpProtocol.csproj` | 版本升级到 1.0.2 |
| `src/McpHost/McpHost.csproj` | NuGet 包引用升级到 1.0.2 |
| `src/Common/Common.csproj` | NuGet 包引用升级到 1.0.2 |
| `nuget.config` | 配置本地 NuGet 源 |
| `build.ps1` | 集成自动打包和缓存清理 |
| `scripts/clear-nuget-cache.ps1` | 新增缓存清理脚本 |
| `.gitignore` | 添加 `nuget/` 目录忽略 |

---

## 验证结果

### E2E测试结果

```
=== Test Results ===
[PASS] Initialize
[PASS] Tools/List - MCP Tools
[PASS] MCP Tool - tool_search
[PASS] MCP Tool - tool_describe
[PASS] MCP Tool - package_status
[PASS] MCP Tool - package_install (nonexistent)
[PASS] CLI Tool - memory_create_entities
[PASS] CLI Tool - memory_search_nodes
[PASS] CLI Tool - memory_read_graph
[PASS] CLI Tool - Create Relations
[PASS] ID Format - String ID
[PASS] ID Format - Large Number ID
[PASS] ID Format - Notification (No ID)
[PASS] Error Handling - Unknown Tool
[PASS] JSON-RPC Format - Error Response
[PASS] Error Handling - Malformed JSON
[PASS] Error Handling - Invalid Parameters
[PASS] Error Handling - Timeout Graceful
[PASS] FileReaderCli - tool_search
[PASS] FileReaderCli - read_head
[PASS] FileReaderCli - read_tail
[PASS] FileReaderCli - Nonexistent File Error
[PASS] Both CLIs - tool_list

Total: 23/23 tests passed ✅
```

---

## 经验总结

### 【成功经验】

1. **本地 NuGet 包开发流程**
   - 使用本地 `nuget/` 目录作为 NuGet 源
   - 每次构建前自动打包并清理缓存
   - 确保 `nuget.config` 中本地源优先级高于远程源

2. **协议版本区分**
   - JSON-RPC 协议版本 (`2.0`) 和 MCP 协议版本 (`2024-11-05`) 是不同的概念
   - 需要明确定义常量，避免混淆

3. **命名空间冲突处理**
   - 当静态类名与命名空间相同时（如 `McpProtocol`），会导致编译错误
   - 解决方案：使用不同的名称（如 `McpProtocolVersion`）

4. **自动化缓存清理**
   - 创建 `scripts/clear-nuget-cache.ps1` 脚本
   - 集成到 `build.ps1` 中，每次构建自动清理
   - 避免手动操作遗漏

### 【避坑指南】

1. **NuGet 缓存陷阱**
   - 修改本地库代码后，必须清理 NuGet 缓存才能生效
   - 使用 `.\build.ps1` 脚本自动处理

2. **版本号同步**
   - 修改 `lib/McpProtocol` 后需要升级版本号
   - 同时更新所有引用项目的版本号

3. **NativeAOT 编译缓存**
   - NativeAOT 编译后需要重新发布才能生效
   - 使用 `.\build.ps1` 脚本进行完整发布

---

## 原始问题记录（已归档）

<details>
<summary>点击展开原始问题详情</summary>

### 1. 协议版本错误

**期望行为**:  
服务器在 `initialize` 响应中返回 `"protocolVersion": "2024-11-05"`

**实际行为**:  
服务器返回 `"protocolVersion": "2.0"` (这是JSON-RPC版本，不是MCP协议版本)

**错误日志**:
```
[CLIENT] {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"E2ETest","version":"1.0"}}}
[SERVER] {"id":1,"result":{"protocolVersion":"2.0","capabilities":{"tools":{"listChanged":false}},"serverInfo":{"name":"McpHost","version":"1.0.0"}},"jsonrpc":"2.0"}
```

**测试失败信息**:
```
[FAIL] Initialize
  Error: Protocol version should match: Expected 2024-11-05, got 2.0
```

### 2. tools/list 方法崩溃

**错误信息**:
```
[ERROR] [2026-04-29 22:07:09.001] [ERR] [McpHost] Server error: Operation is not valid due to the current state of the object.
```

**堆栈跟踪**:
```
at System.Text.Json.Serialization.Converters.JsonElementConverter.Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
at McpProtocol.Contracts.McpJsonContext.ToolDefinitionSerializeHandler(Utf8JsonWriter writer, ToolDefinition value)
```

</details>

---

## 参考资料

- [NativeAOT文档](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [JsonElement序列化](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonelement)
- [MCP协议规范](https://modelcontextprotocol.io/)
