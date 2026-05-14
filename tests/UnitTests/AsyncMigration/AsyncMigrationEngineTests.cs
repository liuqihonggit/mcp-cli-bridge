using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AstCli.Services;

namespace MyMemoryServer.UnitTests.AsyncMigration;

public sealed class AsyncMigrationEngineTests
{
    private static string RewriteCode(string code, CSharpSyntaxRewriter rewriter)
    {
        var tree = CSharpSyntaxTree.ParseText(code, path: "test.cs");
        var root = tree.GetCompilationUnitRoot();
        var newRoot = rewriter.Visit(root);
        return newRoot.ToFullString();
    }

    #region async_rename - 方法重命名 SendLog → SendLogAsync

    [Fact]
    public void AsyncRename_SingleMethod_ShouldRename()
    {
        var code = """
                   public class Foo
                   {
                       public void SendLog(string msg) { }
                   }
                   """;
        var result = RewriteCode(code, new MethodRenameRewriter("SendLog", "SendLogAsync"));
        result.Should().Contain("SendLogAsync");
        result.Should().NotContain("SendLog(");
    }

    [Fact]
    public void AsyncRename_ShouldAlsoRenameCallSites()
    {
        var code = """
                   public class Foo
                   {
                       public void SendLog(string msg) { }
                       public void Bar() { SendLog("hello"); }
                   }
                   """;
        var result = RewriteCode(code, new MethodRenameRewriter("SendLog", "SendLogAsync"));
        result.Should().Contain("SendLogAsync");
        result.Should().NotContain("SendLog(");
    }

    [Fact]
    public void AsyncRename_ShouldNotRenamePartialMatch()
    {
        var code = """
                   public class Foo
                   {
                       public void SendLog(string msg) { }
                       public void SendLogExtra(string msg) { }
                   }
                   """;
        var result = RewriteCode(code, new MethodRenameRewriter("SendLog", "SendLogAsync"));
        result.Should().Contain("SendLogAsync");
        result.Should().Contain("SendLogExtra");
    }

    [Fact]
    public void AsyncRename_NoMatch_ShouldNotChange()
    {
        var code = """
                   public class Foo
                   {
                       public void Other() { }
                   }
                   """;
        var result = RewriteCode(code, new MethodRenameRewriter("SendLog", "SendLogAsync"));
        result.Should().Contain("Other");
    }

    #endregion

    #region async_add_modifier - 加 async 关键字

    [Fact]
    public void AsyncAddModifier_SyncMethod_ShouldAddAsync()
    {
        var code = """
                   public class Foo
                   {
                       public Task SendLogAsync(string msg) { }
                   }
                   """;
        var result = RewriteCode(code, new AsyncModifierRewriter("SendLogAsync"));
        result.Should().Contain("public async Task SendLogAsync");
    }

    [Fact]
    public void AsyncAddModifier_AlreadyAsync_ShouldNotDuplicate()
    {
        var code = """
                   public class Foo
                   {
                       public async Task SendLogAsync(string msg) { }
                   }
                   """;
        var result = RewriteCode(code, new AsyncModifierRewriter("SendLogAsync"));
        result.Should().NotContain("async async");
    }

    [Fact]
    public void AsyncAddModifier_NoMatch_ShouldNotChange()
    {
        var code = """
                   public class Foo
                   {
                       public void Other() { }
                   }
                   """;
        var result = RewriteCode(code, new AsyncModifierRewriter("SendLogAsync"));
        result.Should().Contain("public void Other");
    }

    #endregion

    #region async_return_type - 改返回类型 void→Task, T→Task<T>

    [Fact]
    public void AsyncReturnType_VoidToTask_ShouldChange()
    {
        var code = """
                   public class Foo
                   {
                       public void SendLogAsync(string msg) { }
                   }
                   """;
        var result = RewriteCode(code, new ReturnTypeRewriter("SendLogAsync"));
        result.Should().Contain("public Task SendLogAsync");
        result.Should().NotContain("public void SendLogAsync");
    }

    [Fact]
    public void AsyncReturnType_StringToTaskString_ShouldWrap()
    {
        var code = """
                   public class Foo
                   {
                       public string GetNameAsync() { return ""; }
                   }
                   """;
        var result = RewriteCode(code, new ReturnTypeRewriter("GetNameAsync"));
        result.Should().Contain("public Task<string> GetNameAsync");
        result.Should().NotContain("public string GetNameAsync");
    }

    [Fact]
    public void AsyncReturnType_AlreadyTask_ShouldNotChange()
    {
        var code = """
                   public class Foo
                   {
                       public Task SendLogAsync(string msg) { }
                   }
                   """;
        var result = RewriteCode(code, new ReturnTypeRewriter("SendLogAsync"));
        result.Should().Contain("public Task SendLogAsync");
    }

    [Fact]
    public void AsyncReturnType_AlreadyTaskGeneric_ShouldNotChange()
    {
        var code = """
                   public class Foo
                   {
                       public Task<string> GetNameAsync() { return null; }
                   }
                   """;
        var result = RewriteCode(code, new ReturnTypeRewriter("GetNameAsync"));
        result.Should().Contain("public Task<string> GetNameAsync");
    }

    [Fact]
    public void AsyncReturnType_IntToTaskInt_ShouldWrap()
    {
        var code = """
                   public class Foo
                   {
                       public int GetCountAsync() { return 0; }
                   }
                   """;
        var result = RewriteCode(code, new ReturnTypeRewriter("GetCountAsync"));
        result.Should().Contain("public Task<int> GetCountAsync");
    }

    #endregion

    #region async_add_await - 加 await + ConfigureAwait

    [Fact]
    public void AsyncAddAwait_SyncInvocation_ShouldAddAwait()
    {
        var code = """
                   public class Foo
                   {
                       public async Task Bar()
                       {
                           SendLogAsync("msg");
                       }
                   }
                   """;
        var result = RewriteCode(code, new AwaitInvocationRewriter("SendLogAsync", addConfigureAwait: false));
        result.Should().Contain("await SendLogAsync");
    }

    [Fact]
    public void AsyncAddAwait_WithConfigureAwait_ShouldAddBoth()
    {
        var code = """
                   public class Foo
                   {
                       public async Task Bar()
                       {
                           SendLogAsync("msg");
                       }
                   }
                   """;
        var result = RewriteCode(code, new AwaitInvocationRewriter("SendLogAsync", addConfigureAwait: true));
        result.Should().Contain("await SendLogAsync(\"msg\").ConfigureAwait(false)");
    }

    [Fact]
    public void AsyncAddAwait_AlreadyAwaited_ShouldNotDuplicate()
    {
        var code = """
                   public class Foo
                   {
                       public async Task Bar()
                       {
                           await SendLogAsync("msg");
                       }
                   }
                   """;
        var result = RewriteCode(code, new AwaitInvocationRewriter("SendLogAsync", addConfigureAwait: false));
        result.Should().NotContain("await await");
    }

    [Fact]
    public void AsyncAddAwait_Assignment_ShouldAddAwait()
    {
        var code = """
                   public class Foo
                   {
                       public async Task Bar()
                       {
                           var result = GetNameAsync();
                       }
                   }
                   """;
        var result = RewriteCode(code, new AwaitInvocationRewriter("GetNameAsync", addConfigureAwait: false));
        result.Should().Contain("var result = await GetNameAsync()");
    }

    [Fact]
    public void AsyncAddAwait_NoMatch_ShouldNotChange()
    {
        var code = """
                   public class Foo
                   {
                       public async Task Bar()
                       {
                           OtherMethod();
                       }
                   }
                   """;
        var result = RewriteCode(code, new AwaitInvocationRewriter("SendLogAsync", addConfigureAwait: false));
        result.Should().Contain("OtherMethod()");
    }

    [Fact]
    public void AsyncAddAwait_WithConfigureAwait_Assignment_ShouldAddBoth()
    {
        var code = """
                   public class Foo
                   {
                       public async Task Bar()
                       {
                           var result = GetNameAsync();
                       }
                   }
                   """;
        var result = RewriteCode(code, new AwaitInvocationRewriter("GetNameAsync", addConfigureAwait: true));
        result.Should().Contain("var result = await GetNameAsync().ConfigureAwait(false)");
    }

    #endregion

    #region async_param_add - 插入参数

    [Fact]
    public void AsyncParamAdd_CancellationToken_ShouldAppend()
    {
        var code = """
                   public class Foo
                   {
                       public Task SendLogAsync(string msg) { }
                   }
                   """;
        var result = RewriteCode(code, new ParameterAddRewriter("SendLogAsync", "CancellationToken", "ct"));
        result.Should().Contain("string msg, CancellationToken ct");
    }

    [Fact]
    public void AsyncParamAdd_MethodWithNoParams_ShouldAddAsFirst()
    {
        var code = """
                   public class Foo
                   {
                       public Task SendLogAsync() { }
                   }
                   """;
        var result = RewriteCode(code, new ParameterAddRewriter("SendLogAsync", "CancellationToken", "ct"));
        result.Should().Contain("SendLogAsync(CancellationToken ct)");
    }

    [Fact]
    public void AsyncParamAdd_NoMatch_ShouldNotChange()
    {
        var code = """
                   public class Foo
                   {
                       public Task Other() { }
                   }
                   """;
        var result = RewriteCode(code, new ParameterAddRewriter("SendLogAsync", "CancellationToken", "ct"));
        result.Should().Contain("Task Other()");
    }

    [Fact]
    public void AsyncParamAdd_AlreadyExists_ShouldNotDuplicate()
    {
        var code = """
                   public class Foo
                   {
                       public Task SendLogAsync(string msg, CancellationToken ct) { }
                   }
                   """;
        var result = RewriteCode(code, new ParameterAddRewriter("SendLogAsync", "CancellationToken", "ct"));
        var count = result.Split("CancellationToken ct").Length - 1;
        count.Should().Be(1, "参数不应重复添加");
    }

    #endregion

    #region sync_remove_modifier - 移除 async 关键字

    [Fact]
    public void SyncRemoveModifier_AsyncMethod_ShouldRemoveAsync()
    {
        var code = """
                   public class Foo
                   {
                       public async Task SendLog(string msg) { }
                   }
                   """;
        var result = RewriteCode(code, new SyncModifierRemoverRewriter("SendLog"));
        result.Should().Contain("public Task SendLog");
        result.Should().NotContain("public async Task SendLog");
    }

    [Fact]
    public void SyncRemoveModifier_NoAsync_ShouldNotChange()
    {
        var code = """
                   public class Foo
                   {
                       public Task SendLog(string msg) { }
                   }
                   """;
        var result = RewriteCode(code, new SyncModifierRemoverRewriter("SendLog"));
        result.Should().Contain("public Task SendLog");
    }

    [Fact]
    public void SyncRemoveModifier_NoMatch_ShouldNotChange()
    {
        var code = """
                   public class Foo
                   {
                       public async Task Other() { }
                   }
                   """;
        var result = RewriteCode(code, new SyncModifierRemoverRewriter("SendLog"));
        result.Should().Contain("public async Task Other");
    }

    #endregion

    #region sync_return_type - Task→void, Task<T>→T

    [Fact]
    public void SyncReturnType_TaskToVoid_ShouldChange()
    {
        var code = """
                   public class Foo
                   {
                       public Task SendLog(string msg) { }
                   }
                   """;
        var result = RewriteCode(code, new SyncReturnTypeRewriter("SendLog"));
        result.Should().Contain("public void SendLog");
        result.Should().NotContain("public Task SendLog");
    }

    [Fact]
    public void SyncReturnType_TaskStringToString_ShouldUnwrap()
    {
        var code = """
                   public class Foo
                   {
                       public Task<string> GetName() { return null; }
                   }
                   """;
        var result = RewriteCode(code, new SyncReturnTypeRewriter("GetName"));
        result.Should().Contain("public string GetName");
        result.Should().NotContain("Task<string>");
    }

    [Fact]
    public void SyncReturnType_TaskIntToInt_ShouldUnwrap()
    {
        var code = """
                   public class Foo
                   {
                       public Task<int> GetCount() { return 0; }
                   }
                   """;
        var result = RewriteCode(code, new SyncReturnTypeRewriter("GetCount"));
        result.Should().Contain("public int GetCount");
    }

    [Fact]
    public void SyncReturnType_Void_ShouldNotChange()
    {
        var code = """
                   public class Foo
                   {
                       public void SendLog(string msg) { }
                   }
                   """;
        var result = RewriteCode(code, new SyncReturnTypeRewriter("SendLog"));
        result.Should().Contain("public void SendLog");
    }

    [Fact]
    public void SyncReturnType_PlainType_ShouldNotChange()
    {
        var code = """
                   public class Foo
                   {
                       public string GetName() { return ""; }
                   }
                   """;
        var result = RewriteCode(code, new SyncReturnTypeRewriter("GetName"));
        result.Should().Contain("public string GetName");
    }

    #endregion

    #region sync_remove_await - 移除 await + ConfigureAwait

    [Fact]
    public void SyncRemoveAwait_AwaitedInvocation_ShouldRemoveAwait()
    {
        var code = """
                   public class Foo
                   {
                       public async Task Bar()
                       {
                           await SendLogAsync("msg");
                       }
                   }
                   """;
        var result = RewriteCode(code, new SyncAwaitRemoverRewriter("SendLogAsync"));
        result.Should().Contain("SendLogAsync(");
        result.Should().NotContain("await SendLogAsync");
    }

    [Fact]
    public void SyncRemoveAwait_AwaitedWithConfigureAwait_ShouldRemoveBoth()
    {
        var code = """
                   public class Foo
                   {
                       public async Task Bar()
                       {
                           await SendLogAsync("msg").ConfigureAwait(false);
                       }
                   }
                   """;
        var result = RewriteCode(code, new SyncAwaitRemoverRewriter("SendLogAsync"));
        result.Should().Contain("SendLogAsync(");
        result.Should().NotContain("await");
        result.Should().NotContain("ConfigureAwait");
    }

    [Fact]
    public void SyncRemoveAwait_Assignment_ShouldRemoveAwait()
    {
        var code = """
                   public class Foo
                   {
                       public async Task Bar()
                       {
                           var result = await GetNameAsync();
                       }
                   }
                   """;
        var result = RewriteCode(code, new SyncAwaitRemoverRewriter("GetNameAsync"));
        result.Should().Contain("var result = GetNameAsync()");
        result.Should().NotContain("await");
    }

    [Fact]
    public void SyncRemoveAwait_AssignmentWithConfigureAwait_ShouldRemoveBoth()
    {
        var code = """
                   public class Foo
                   {
                       public async Task Bar()
                       {
                           var result = await GetNameAsync().ConfigureAwait(false);
                       }
                   }
                   """;
        var result = RewriteCode(code, new SyncAwaitRemoverRewriter("GetNameAsync"));
        result.Should().Contain("var result = GetNameAsync()");
        result.Should().NotContain("await");
        result.Should().NotContain("ConfigureAwait");
    }

    [Fact]
    public void SyncRemoveAwait_NoMatch_ShouldNotChange()
    {
        var code = """
                   public class Foo
                   {
                       public async Task Bar()
                       {
                           await OtherAsync();
                       }
                   }
                   """;
        var result = RewriteCode(code, new SyncAwaitRemoverRewriter("SendLogAsync"));
        result.Should().Contain("await OtherAsync");
    }

    #endregion

    #region sync_param_remove - 移除参数

    [Fact]
    public void SyncParamRemove_ShouldRemoveParam()
    {
        var code = """
                   public class Foo
                   {
                       public Task SendLogAsync(string msg, CancellationToken ct) { }
                   }
                   """;
        var result = RewriteCode(code, new ParameterRemoveRewriter("SendLogAsync", "ct"));
        result.Should().Contain("SendLogAsync(string msg)");
        result.Should().NotContain("CancellationToken");
    }

    [Fact]
    public void SyncParamRemove_OnlyParam_ShouldLeaveEmpty()
    {
        var code = """
                   public class Foo
                   {
                       public Task SendLogAsync(CancellationToken ct) { }
                   }
                   """;
        var result = RewriteCode(code, new ParameterRemoveRewriter("SendLogAsync", "ct"));
        result.Should().Contain("SendLogAsync()");
        result.Should().NotContain("CancellationToken");
    }

    [Fact]
    public void SyncParamRemove_NoMatch_ShouldNotChange()
    {
        var code = """
                   public class Foo
                   {
                       public Task Other(string msg) { }
                   }
                   """;
        var result = RewriteCode(code, new ParameterRemoveRewriter("SendLogAsync", "ct"));
        result.Should().Contain("Task Other(string msg)");
    }

    [Fact]
    public void SyncParamRemove_ParamNotFound_ShouldNotChange()
    {
        var code = """
                   public class Foo
                   {
                       public Task SendLogAsync(string msg) { }
                   }
                   """;
        var result = RewriteCode(code, new ParameterRemoveRewriter("SendLogAsync", "ct"));
        result.Should().Contain("SendLogAsync(string msg)");
    }

    #endregion
}
