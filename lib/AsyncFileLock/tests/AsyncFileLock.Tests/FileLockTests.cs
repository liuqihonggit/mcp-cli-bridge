using FluentAssertions;
using Xunit;

namespace AsyncFileLock.Tests;

public sealed class FileLockTests : IDisposable
{
    private readonly string _testDir;

    public FileLockTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"AsyncFileLockTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private string GetTestFile(string name) => Path.Combine(_testDir, name);

    [Fact]
    public async Task AcquireAsync_SingleFile_ShouldSucceed()
    {
        var filePath = GetTestFile("test.json");

        var result = await FileLockService.AcquireAsync(filePath, TimeSpan.FromSeconds(5));

        result.Success.Should().BeTrue();
        result.Lock.Should().NotBeNull();
        result.Lock!.FilePaths.Count.Should().Be(1);
    }

    [Fact]
    public async Task AcquireBatchAsync_MultipleFiles_ShouldSucceed()
    {
        var filePaths = new[]
        {
            GetTestFile("file1.json"),
            GetTestFile("file2.json"),
            GetTestFile("file3.json")
        };

        var result = await FileLockService.AcquireBatchAsync(filePaths, TimeSpan.FromSeconds(5));

        result.Success.Should().BeTrue();
        result.Lock.Should().NotBeNull();
        result.Lock!.FilePaths.Count.Should().Be(3);
    }

    [Fact]
    public async Task AcquireBatchAsync_DuplicateFiles_ShouldDeduplicate()
    {
        var filePath = GetTestFile("duplicate.json");
        var filePaths = new[] { filePath, filePath, filePath };

        var result = await FileLockService.AcquireBatchAsync(filePaths, TimeSpan.FromSeconds(5));

        result.Success.Should().BeTrue();
        result.Lock!.FilePaths.Count.Should().Be(1);
    }

    [Fact]
    public async Task AcquireBatchAsync_EmptyList_ShouldFail()
    {
        var result = await FileLockService.AcquireBatchAsync([], TimeSpan.FromSeconds(5));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No files specified");
    }

    [Fact]
    public async Task BatchLock_Dispose_ShouldReleaseAll()
    {
        var filePaths = new[]
        {
            GetTestFile("release1.json"),
            GetTestFile("release2.json")
        };

        var result = await FileLockService.AcquireBatchAsync(filePaths, TimeSpan.FromSeconds(5));
        result.Success.Should().BeTrue();

        await result.Lock!.DisposeAsync();

        // 验证锁已释放，可以重新获取
        var result2 = await FileLockService.AcquireBatchAsync(filePaths, TimeSpan.FromSeconds(5));
        result2.Success.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireBatchAsync_ConcurrentAccess_ShouldBlock()
    {
        var filePaths = new[]
        {
            GetTestFile("concurrent1.json"),
            GetTestFile("concurrent2.json")
        };

        var result1 = await FileLockService.AcquireBatchAsync(filePaths, TimeSpan.FromSeconds(5));
        result1.Success.Should().BeTrue();

        // 尝试从另一个任务获取相同的锁
        var secondAcquired = false;
        var secondTask = Task.Run(async () =>
        {
            try
            {
                var result2 = await FileLockService.AcquireBatchAsync(filePaths, TimeSpan.FromMilliseconds(200));
                secondAcquired = result2.Success;
            }
            catch
            {
            }
        });

        await secondTask;
        secondAcquired.Should().BeFalse();
    }

    [Fact]
    public async Task AcquireBatchAsync_AsyncWorkflow_CanAwaitInsideLock()
    {
        var filePath = GetTestFile("async_workflow.json");
        var result = 0;

        var lockResult = await FileLockService.AcquireAsync(filePath, TimeSpan.FromSeconds(5));
        lockResult.Success.Should().BeTrue();

        await using (var batchLock = lockResult.Lock)
        {
            await Task.Delay(50);
            result = 42;
            await Task.Delay(50);
        }

        result.Should().Be(42);
    }

    [Fact]
    public async Task MultipleSequentialAcquires_ShouldWork()
    {
        var filePath = GetTestFile("sequential.json");

        for (int i = 0; i < 10; i++)
        {
            var result = await FileLockService.AcquireAsync(filePath, TimeSpan.FromSeconds(5));
            result.Success.Should().BeTrue();
            await result.Lock!.DisposeAsync();
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task ConcurrentDifferentFiles_ShouldNotBlock()
    {
        var filePath1 = GetTestFile("file1.json");
        var filePath2 = GetTestFile("file2.json");

        var result1 = await FileLockService.AcquireAsync(filePath1, TimeSpan.FromSeconds(5));
        var result2 = await FileLockService.AcquireAsync(filePath2, TimeSpan.FromSeconds(5));

        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
    }

    [Fact]
    public async Task HeavyConcurrentLoad_ShouldHandleCorrectly()
    {
        var filePath = GetTestFile("heavy_load.json");
        var counter = 0;
        var tasks = new List<Task>();
        const int taskCount = 20;

        for (int i = 0; i < taskCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var result = await FileLockService.AcquireAsync(filePath, TimeSpan.FromSeconds(10));
                if (result.Success)
                {
                    await using (result.Lock)
                    {
                        Interlocked.Increment(ref counter);
                        await Task.Delay(10);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);
        counter.Should().Be(taskCount);
    }

    [Fact]
    public async Task BatchLock_DeadlockPrevention_OrderedAcquisition()
    {
        // 测试：两个任务以不同顺序请求相同的文件，应该能正确获取（不会死锁）
        var filePaths1 = new[]
        {
            GetTestFile("deadlock_a.json"),
            GetTestFile("deadlock_b.json")
        };

        var filePaths2 = new[]
        {
            GetTestFile("deadlock_b.json"), // 反向顺序
            GetTestFile("deadlock_a.json")
        };

        var result1 = await FileLockService.AcquireBatchAsync(filePaths1, TimeSpan.FromSeconds(5));
        result1.Success.Should().BeTrue();

        // 第二个任务应该被阻塞，而不是死锁
        var secondAcquired = false;
        var secondTask = Task.Run(async () =>
        {
            var result2 = await FileLockService.AcquireBatchAsync(filePaths2, TimeSpan.FromMilliseconds(500));
            secondAcquired = result2.Success;
        });

        await secondTask;

        // 第二个任务应该超时（被阻塞），而不是死锁
        secondAcquired.Should().BeFalse();
    }
}
