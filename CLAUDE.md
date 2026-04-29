# CLAUDE.md - AI行为红线手册

## 🔴 绝对禁止（触碰即错）

### 代码编写禁令

1. **❌ 禁止直接解析JSON字符串**
   - 错误: `JsonSerializer.Deserialize<dynamic>(json)`
   - 原因: NativeAOT不支持动态类型，会导致编译失败
   - 正确: 必须用类表示，通过 `CommonJsonContext.Default.MyType` 序列化
2. **❌ 禁止在.cs文件内写using语句**
   - 原因: 违反GlobalUsings统一管理原则
   - 正确: 所有命名空间引用必须放在 `GlobalUsings.cs`
3. **❌ 禁止硬编码**
   - 禁止 if-else 链判断类型
   - 禁止魔法数字和字符串
   - 禁止硬编码数组（如 `new[] { "add", "commit" }`）
   - 正确: 使用 `FrozenDictionary` / 特性 / `nameof()` / `typeof()`
4. **❌ 禁止多参数方法**
   - 错误: `public void Test(string cmd, string args, string options)`
   - 原因: 解析不应发生在总线
   - 正确: 封装为类 `public void Test(Command cmd)`
5. **❌ 禁止运行时反射emit和动态代码生成**
   - 原因: NativeAOT不支持
   - 正确: 使用源码生成器或直接写死
6. **❌ 禁止在接口/契约层添加具体实现**
   - 原因: 契约层只能有接口/抽象和纯DTO
   - 正确: 实现放到具体服务层
   - 📎 *详细约束见 AGENTS.md > 架构依赖关系*

### 操作禁令

1. **❌ 禁止删除文件**
   - 错误: `Remove-Item`, `del`, 文件删除操作
   - 正确: 必须改为 `Move-Item` 移动到 `.x/` 目录
   - 格式: `.x/{原有文件名}.{原有后缀}.del`
2. **❌ 禁止使用PowerShell Set-Content修改C#文件**
   - 错误编号: CS1022
   - 原因: 可能导致文件损坏
   - 正确: 使用 `SearchReplace` 工具修改文件内容
3. **❌ 禁止使用会卡住交互的命令**
   - 禁止: `more`, `less` 等分页命令
   - 禁止: `git commit` 不带 `-m` 参数（会打开编辑器）
   - 禁止: `npm init` 等交互式命令（使用 `-y` 跳过）
   - 禁止: PowerShell 的 `Out-Host -Paging`
   - 推荐: 使用 `| Select-Object -First N` 替代分页
4. **❌ 禁止猜测用户意图/背景/业务场景**（任务规划层面）
   - 任务规划首先阅读: `AI交互文档/任务并行化.md`
   - 禁止假设用户的具体业务需求、行业背景或隐含意图
   - 一旦信息存在模糊地带，基于行业最佳实践自主选择技术方案
   - 优先选择保守、安全的实现方式
   - 记录决策依据到工作文件末尾（遵循第3条对话偏好）
   - 此时推荐使用 `AskUserQuestion` 工具，询问用户是否确认，或者建议其他实现方式
5. **❌ 禁止使用任何交互式工具**（无人值守层面）
   - 禁止: `AskUserQuestion` 工具（会阻塞等待用户响应）
   - 禁止: 任何需要用户输入确认的操作
   - 原因: 必须支持无人值守模式，任务必须自动完成
   - 正确: 自主决策技术方案，基于上下文选择最合理的实现方式继续执行
   - 正确: 遇到模糊地带时，选择行业最佳实践或最保守方案
   - 正确: 一旦错误非常巨大，通过本文要求，评估/回滚/渐进式修复

***

## ✅ 必须执行（遗漏即错）

### 开发流程强制要求

1. **✅ 必须采用渐进式开发方法**
   - 每次只完成一个功能，不要一次完成所有功能
   - 每完成一个功能都要编译成功后提交git
   - 主工程编译成功后，测试用例也需编译成功
   - 一旦有疑问或发现错误，**立即停止**，先修复再继续
   - 🔗 **例外**: `/spec` 模式下允许并行处理多个文件分区操作，详见 `AI交互文档/任务并行化.md`

1.5. **⚠️ 任务失败处理机制（唯一例外）**

### 📊 错误分级（必须先评估，禁止立即回滚）

| 级别          | 处理方式     | 示例                |
| ----------- | -------- | ----------------- |
| ⚪ **小错误**   | 直接修复，不回滚 | 语法错误、拼写、缺少引用      |
| 🟡 **中等问题** | 调整方案，不回滚 | 接口不匹配、类型冲突        |
| 🔴 **严重问题** | 回滚+切分任务  | 架构冲突、NativeAOT不兼容 |

### 🔧 执行流程（共3次重试机会）

1. **阶段1 - 自行修复**（小/中等问题）
   - 不回滚git，直接在当前代码基础上修复
   - 同一错误最多尝试 **2次**
   - 失败 → 进入阶段2
2. **阶段2 - 回滚重试**（严重问题或修复失败）
   ```powershell
   git --no-pager checkout -- .
   git --no-pager clean -fd
   ```
   - 切分任务为更小的子任务，重新执行
   - 最多 **1次** 回滚重试机会
   - 失败 → 进入阶段3
3. **阶段3 - 最终裁决**（所有重试用尽）
   - ❌ 立即停止，记录失败报告，交给用户决策

### ⚠️ 核心约束

- ✅ **必须先评估**：禁止遇到小错误就立即回滚
- ✅ **总上限3次**：含自行修复和回滚重试
- ✅ **每次不同方案**：禁止重复相同操作
- ❌ 超过3次后**禁止继续尝试**

### 📝 失败记录格式（最终裁决时使用）

```markdown
<!-- 🛑 Task Failed: [任务名称] -->
<!-- 尝试: 3/3 | 时间: 2026-04-30 -->
<!-- 原因: [具体错误] -->
<!-- 已尝试: 方案1→[结果], 方案2→[结果], 方案3→[结果] -->
<!-- 需要决策: [选项A/B/C] -->
```

1. **✅ 必须保证Git环境干净后再开始工作**
   - 开始任务前先备份一次
   - 所有git操作必须使用无分页模式:
     - `git --no-pager log`
     - `git --no-pager diff`
     - `git --no-pager status`
   - 或设置环境变量: `$env:GIT_PAGER='cat'` (PowerShell)
2. **✅ 必须支持 NativeAOT 兼容编译**
   - 所有代码必须兼容 NativeAOT 编译
   - 禁止使用动态类型、反射 emit、直接解析 JSON
   - 📎 *详细要求和发布约束见 AGENTS.md > 关键约束*

### 代码质量强制要求

1. **✅ 所有异步操作必须传入CancellationToken**
   - 包括文件IO、网络请求、进程管理等
   - 用于超时控制和资源释放
2. **✅ 每个外部请求调用都要try-catch并记录日志**
   - 全局拦截异常，带日志栈帧标记
   - 不能吞掉异常，必须向上传播或转换
3. **✅ 参数封装为类**
   - 避免超过3个参数的方法
   - 每个任务执行后检查是否可以封装参数
4. **✅ 使用类型安全引用**
   - 使用 `typeof()` 而非字符串
   - 使用 `nameof()` 而非硬编码名称
5. **✅ 必须使用 LINQ 链式编程**
   - 优先使用 `.Select()`, `.Where()`, `.OrderBy()` 等链式方法
   - **❌ 禁止使用** **`Parallel.ForEach`**（难以调试、易死锁、不支持异步）
   - 正确示例: `items.Select(x => Process(x)).ToList()`
   - 错误示例: `Parallel.ForEach(items, item => Process(item))`

### 🔴 C# 异步编程红线（触碰即错）

#### 绝对禁止

1. **❌ 禁止** **`async void`**（除 UI 事件处理器外）
   - **原因**: 异常无法被捕获，导致应用崩溃且难以调试
   - **正确**: 始终返回 `Task` 或 `Task<T>`
   ```csharp
   // ❌ 错误: 异常会直接炸掉进程
   public async void ProcessData() { ... }

   // ✅ 正常: 异常可被调用者捕获
   public async Task ProcessDataAsync() { ... }
   ```
2. **❌ 禁止** **`.Result`** **/** **`.Wait()`** **阻塞调用**
   - **原因**: 极易导致死锁，尤其在有 SynchronizationContext 的环境
   - **正确**: 保持全异步调用链，始终使用 `await`
   ```csharp
   // ❌ 致命: 100% 会死锁
   var data = GetDataAsync().Result;

   // ✅ 安全: 保持异步传播
   var data = await GetDataAsync();
   ```
3. **❌ 禁止** **`Task.Run`** **包装 I/O 异步方法**
   - **原因**: I/O 本身就是异步的，Task.Run 浪费线程池线程
   - **正确**: 直接 await 原生异步方法
   ```csharp
   // ❌ 错误: 用线程池等 I/O 完成，纯属浪费
   var result = await Task.Run(() => httpClient.GetStringAsync(url));

   // ✅ 正确: 直接异步等待
   var result = await httpClient.GetStringAsync(url);
   ```
   - **例外**: 仅 CPU 密集型工作（如 JSON 解析、图像处理）才用 Task.Run
4. **❌ 禁止在循环中逐个** **`await`（需要并发时）**
   - **原因**: 串行执行，性能极差
   - **正确**: 使用 `Task.WhenAll` 并发执行
   ```csharp
   // ❌ 串行: 总耗时 = sum(每个任务时间)
   foreach (var item in items) { await ProcessAsync(item); }

   // ✅ 并发: 总耗时 = max(每个任务时间)
   await Task.WhenAll(items.Select(x => ProcessAsync(x)));
   ```

#### 强制要求

1. **✅ 所有异步方法必须传递** **`CancellationToken`**
   - 包括文件IO、网络请求、进程管理等
   - 用于超时控制和优雅停止
   ```csharp
   public async Task<string> FetchDataAsync(string url, CancellationToken ct)
   {
       return await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
   }
   ```
2. **✅ 库代码必须使用** **`ConfigureAwait(false)`**
   - **原因**: 避免捕获 SynchronizationContext，减少 15-20% 延迟
   - **适用场景**: 类库项目、底层工具方法、中间件等不依赖同步上下文的代码
   - **例外**: UI 应用（WPF/WinForms）的主线程代码可省略,通常推荐为 Avalonia 应用进行AOT编译.
   - 📎 *项目特定适用范围见 AGENTS.md > 异步编程适用场景*
3. **✅ 异步资源必须正确释放**
   - 使用 `await using` 或 `using` + `DisposeAsync()`
   - **禁止**: 未 await 就丢弃异步资源
   ```csharp
   // ✅ 正确: 异步释放
   await using var stream = new FileStream(path, FileMode.Open);

   // ❌ 错误: 同步 Dispose 可能未完成异步操作
   using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 4096, true);
   ```
4. **✅ 并发异常必须单独处理**
   - `Task.WhenAll` 抛出 `AggregateException`，需逐个处理
   ```csharp
   var tasks = items.Select(async item =>
   {
       try { await ProcessAsync(item).ConfigureAwait(false); }
       catch (Exception ex) { _logger.LogError(ex, $"处理 {item} 失败"); }
   }).ToList();
   await Task.WhenAll(tasks);
   ```

#### 性能优化建议

1. **避免不必要的状态机开销**
   - 若方法直接返回已完成的 Task，无需加 `async`
   ```csharp
   // ❌ 不必要的状态机
   public async Task<int> GetValueAsync() => await Task.FromResult(42);

   // ✅ 无状态机开销
   public Task<int> GetValueAsync() => Task.FromResult(42);
   ```
2. **高频同步完成场景考虑** **`ValueTask<T>`**
   - 减少内存分配（50%+ 提升）
   - 适用于缓存命中、快速路径等
   ```csharp
   public ValueTask<int> GetCachedValueAsync(string key)
   {
       if (_cache.TryGetValue(key, out var value))
           return new ValueTask<int>(value); // 无分配
       return new ValueTask<int>(FetchFromDbAsync(key));
   }
   ```
3. **批量并行处理使用** **`Parallel.ForEachAsync`**（.NET 6+）
   - 比 `Task.WhenAll` 更可控（限制并发度）
   ```csharp
   await Parallel.ForEachAsync(items,
       new ParallelOptions { MaxDegreeOfParallelism = 4 },
       async (item, token) => await ProcessAsync(item, token));
   ```

## 🔄 强制性工作流程

### 经验复用机制（先查后做）

1. **查记忆（开始任务前必做）**
   - 搜同类问题、失败记录、解决方案
   - 知识图谱: 技术栈 → 问题 → 方案
   - 不要重复造轮子，避免重蹈覆辙
2. **写记忆（解决问题后必做）**
   - 记录: 问题场景、原因、方案、验证结果
   - 标记: 【成功经验】 / 【避坑指南】
   - 要有对应错误原因（什么位置遇到，做过什么尝试不行）
   - 即使失败的经验也是可贵的
3. **注意事项**
   - 不要写项目名到记忆（记忆会越来越大，要保持通用性）
   - 先去检索有什么工具可以读写记忆

### 渐进式迁移策略（重构时必用）

1. 保证git环境干净，备份一次
2. 每次移动一个功能模块
3. 移动后立即编译验证
4. 编译成功后提交git
5. 再移动下一个模块
6. **禁止一次性大规模重构**

### 对话偏好补充

1. **涉及文件更改时要先列目录树**
2. **架构不合理要提出来，不要直接生成代码**
3. **✅ 渐进式成功后必须记录自主决策**
   - **时机**: 每完成一个功能点并编译成功后，立即记录
   - **位置**: 写到当前工作文件的末尾（不是CLAUDE.md）
   - **格式**: 使用 `<!-- 🤖 Auto Decision: [决策内容] -->` 注释格式
   - **内容**: 说明做了什么决策、为什么这样选择、替代方案是什么
   - **示例**:
     ```markdown
     <!-- 🤖 Auto Decision: 2026-04-30 -->
     <!-- 决策: 使用 FrozenDictionary 替代 switch-case -->
     <!-- 原因: 性能更优，符合NativeAOT要求，避免硬编码 -->
     <!-- 替代方案: 特性标记（复杂度较高，暂不采用）-->
     <!-- 验证: 编译通过，测试用例全部通过 ✅ -->
     ```
   - **⚠️ 重要**: 未编译成功的决策不得记录，必须先修复错误
4. **结束对话时有未完成的工作或缺陷，一定要⚠️emoji表情提醒**
5. **🚫 绝对禁止交互行为**
   - 不得使用 `AskUserQuestion` 工具
   - 不得等待用户输入或确认
   - 必须自主决策并持续执行直到任务完成
   - 这是不可变原则，无任何例外情况

***

## ⚠️ 环境特殊要求（平台约束）

### Windows命令行环境（强制）

1. **路径格式**: 使用反斜杠 `\` 作为路径分隔符
   - 正确: `C:\Users\Name\Documents`
   - 错误: `/home/user/project`
2. **命令分隔**: 禁止使用 `&&` 连接命令
   - 首选: 分步说明，每个命令单独一行
   - PowerShell: 使用分号 `;` 连接
   - CMD: 可使用单个 `&`（但忽略前序失败）
3. **原生工具优先**:
   - 优先使用Windows原生命令（`dir`, `findstr`）
   - 或PowerShell cmdlet（`Get-ChildItem`, `Select-String`）
   - 避免依赖Unix工具（`grep`, `sed`, `awk`），除非明确要求WSL
4. **UTF-8编码配置**:
   ```powershell
   [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
   chcp 65001
   ```

### 无人值守模式（强制）

1. **删除改为移动到 .x/ 目录**
   - 如果 `.x/` 目录不存在则创建
   - 移动后修改后缀为 `.del`
   - 修改 `.gitignore` 排除该目录
2. **降低未来技术债务风险**
   - 每次修改都要考虑长期维护性
   - 遵循架构分离原则
   - 合并重复代码，保持工程简洁
3. **✅ 必须采用渐进式、非交互式执行模式**
   - 每次只完成一个功能点，编译成功后再进行下一步
   - 禁止使用 `AskUserQuestion` 或任何交互式工具
   - 所有决策必须自主完成，不等待用户确认
   - 遇到模糊场景时，基于最佳实践自主选择方案
   - 任务必须从开始到结束全自动完成，无任何人工干预点

***

## 🛡️ 安全红线

### 文件访问控制

1. 多进程环境使用 `FileShare.Read` 只读共享模式
2. 多线程读写文件使用 `ReaderWriterLockSlim`（lock无法锁定文件写入）
3. 所有文件操作必须传入 CancellationToken 超时控制

### 超时控制（强制标准）

| 操作类型   | 超时时间 | 说明      |
| ------ | ---- | ------- |
| 文件IO操作 | 5秒   | 读写文件    |
| 锁获取    | 5秒   | 避免死锁    |
| 插件加载   | 10秒  | CLI插件启动 |
| MQ操作   | 10秒  | 消息队列通讯  |

### 异常处理规范

1. 超时异常必须记录发生原因和上下文
2. 超时后必须释放已获取的资源
3. **禁止吞掉超时异常**，必须向上传播或转换

### 死锁防护

1. **必须排序后获取锁**，避免循环等待
2. **所有锁必须提供超时机制**
3. **锁获取失败必须记录日志**

```csharp
// ❌ 错误：无序获取（可能导致死锁）
lock (resourceA) { lock (resourceB) { } }
lock (resourceB) { lock (resourceA) { } }

// ✅ 正确：排序后获取
var ordered = new[] { resourceA, resourceB }.OrderBy(r => r.Id);
foreach (var r in ordered) { /* 获取锁 */ }
```

