# McpProtocol

独立的 MCP (Model Context Protocol) 协议 NuGet 包，支持 NativeAOT 编译。

## 项目结构

```
lib/McpProtocol/
├── Directory.Build.props                    # 全局编译配置（强制Native AOT）
└── src/
    ├── McpProtocol.Contracts/               # MCP协议契约
    │   ├── McpProtocol.Contracts.csproj
    │   ├── GlobalUsings.cs
    │   ├── McpJsonContext.cs                # AOT JSON上下文
    │   ├── Models/                          # 协议模型
    │   │   ├── JsonRpcModels.cs
    │   │   ├── InitializeModels.cs
    │   │   ├── ToolModels.cs
    │   │   └── CallToolModels.cs
    │   └── Constants/                       # 常量定义
    │       └── JsonRpcConstants.cs
    │
    └── McpProtocol/                         # MCP协议实现
        ├── McpProtocol.csproj
        ├── GlobalUsings.cs
        ├── McpJsonSerializer.cs             # JSON序列化器
        ├── IMcpServer.cs                    # MCP服务接口
        ├── IToolHandler.cs                  # 工具处理器接口
        └── McpServer.cs                     # MCP服务实现
```

## 安装

### 通过 NuGet 安装

```bash
dotnet add package McpProtocol
dotnet add package McpProtocol.Contracts
```

## 使用方法

### 1. 创建工具处理器

```csharp
using McpProtocol;

public class MyTool : IToolHandler
{
    public string Name => "my_tool";
    public string Description => "My custom tool";

    public async Task<object> ExecuteAsync(Dictionary<string, JsonElement> arguments)
    {
        // 实现工具逻辑
        return "Tool executed successfully";
    }
}
```

### 2. 创建 MCP 服务器

```csharp
using McpProtocol;

var server = new McpServer("MyServer", "1.0.0");
server.RegisterToolHandler(new MyTool());

await server.RunAsync();
```

### 3. 使用 JSON 序列化器

```csharp
using McpProtocol;

var request = new JsonRpcRequest
{
    Id = 1,
    Method = "initialize",
    Params = new InitializeRequestParams()
};

var json = McpJsonSerializer.Serialize(request);
```

## 特性

- ✅ **NativeAOT 支持** - 完全支持 NativeAOT 编译
- ✅ **独立 NuGet 包** - 不依赖主项目，可独立使用
- ✅ **AOT 兼容的 JSON 序列化** - 使用 Source Generator 生成序列化代码
- ✅ **轻量级设计** - 最小化依赖，只包含核心协议功能
- ✅ **契约分离** - Contracts 项目只包含接口和模型，符合 .NET 标准实践

## 架构说明

### McpProtocol.Contracts

契约层，只包含：
- 协议模型（DTO）
- 接口定义
- 常量定义
- AOT JSON 上下文

**纯 DTO（只有属性，没有业务逻辑），符合 Contracts 项目的定位。**

### McpProtocol

实现层，包含：
- MCP 服务实现
- JSON 序列化器
- 工具处理器接口

## 发布 NuGet 包

```bash
# 编译
dotnet build lib\McpProtocol\src\McpProtocol.Contracts\McpProtocol.Contracts.csproj -c Release
dotnet build lib\McpProtocol\src\McpProtocol\McpProtocol.csproj -c Release

# 打包
dotnet pack lib\McpProtocol\src\McpProtocol.Contracts\McpProtocol.Contracts.csproj -c Release
dotnet pack lib\McpProtocol\src\McpProtocol\McpProtocol.csproj -c Release
```

生成的 NuGet 包位于：
- `lib\McpProtocol\src\McpProtocol\bin\Release\McpProtocol.1.0.0.nupkg`
- `lib\McpProtocol\src\McpProtocol.Contracts\bin\Release\McpProtocol.Contracts.1.0.0.nupkg`

## License

MIT
