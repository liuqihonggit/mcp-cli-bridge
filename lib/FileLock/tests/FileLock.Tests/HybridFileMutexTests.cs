namespace FileLock.Tests;

public sealed class HybridFileMutexTests : IDisposable
{
    private readonly string _testDir;

    public HybridFileMutexTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"HybridFileMutexTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        HybridFileMutex.Configure(
            Path.Combine(_testDir, "locks"),
            TimeSpan.FromSeconds(30));
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private string GetTestFile(string name) => Path.Combine(_testDir, name);

    [Fact]
    public async Task Acquire_SingleFile_ShouldSucceed()
    {
        var filePath = GetTestFile("single.json");

        var mutex = HybridFileMutex.Acquire(filePath, TimeSpan.FromSeconds(5));
        mutex.Should().NotBeNull();
        mutex.FilePath.Should().Be(Path.GetFullPath(filePath));
        await mutex.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_SingleFile_ShouldSucceed()
    {
        var filePath = GetTestFile("async_single.json");

        await using var mutex = await HybridFileMutex.AcquireAsync(filePath, TimeSpan.FromSeconds(5));
        mutex.Should().NotBeNull();
        mutex.FilePath.Should().Be(Path.GetFullPath(filePath));
    }

    [Fact]
    public async Task Acquire_AndRelease_ShouldAllowReacquire()
    {
        var filePath = GetTestFile("release.json");

        var mutex1 = HybridFileMutex.Acquire(filePath, TimeSpan.FromSeconds(5));
        mutex1.Release();

        var mutex2 = HybridFileMutex.Acquire(filePath, TimeSpan.FromSeconds(5));
        mutex2.Release();

        await Task.CompletedTask;
    }

    [Fact]
    public async Task AcquireAsync_AndDisposeAsync_ShouldAllowReacquire()
    {
        var filePath = GetTestFile("async_release.json");

        var mutex1 = await HybridFileMutex.AcquireAsync(filePath, TimeSpan.FromSeconds(5));
        await mutex1.DisposeAsync();

        await using var mutex2 = await HybridFileMutex.AcquireAsync(filePath, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Acquire_SameProcessTwoTasks_SecondShouldTimeout()
    {
        var filePath = GetTestFile("intra_process.json");

        var mutex1 = HybridFileMutex.Acquire(filePath, TimeSpan.FromSeconds(5));

        var secondAcquired = false;
        var secondTask = Task.Run(() =>
        {
            try
            {
                HybridFileMutex.Acquire(filePath, TimeSpan.FromMilliseconds(200));
                secondAcquired = true;
            }
            catch (TimeoutException)
            {
            }
        });

        await secondTask;
        secondAcquired.Should().BeFalse("second task should timeout because first task holds the lock");
        mutex1.Release();
    }

    [Fact]
    public async Task AcquireAsync_SameProcessTwoTasks_SecondShouldTimeout()
    {
        var filePath = GetTestFile("async_intra.json");

        await using var mutex1 = await HybridFileMutex.AcquireAsync(filePath, TimeSpan.FromSeconds(5));

        var secondAcquired = false;
        var secondTask = Task.Run(async () =>
        {
            try
            {
                await HybridFileMutex.AcquireAsync(filePath, TimeSpan.FromMilliseconds(200));
                secondAcquired = true;
            }
            catch (TimeoutException)
            {
            }
        });

        await secondTask;
        secondAcquired.Should().BeFalse("second async task should timeout because first task holds the lock");
    }

    [Fact]
    public async Task Acquire_AfterRelease_SecondTaskShouldSucceed()
    {
        var filePath = GetTestFile("after_release.json");

        var mutex1 = HybridFileMutex.Acquire(filePath, TimeSpan.FromSeconds(5));
        mutex1.Release();

        var mutex2 = await Task.Run(async () =>
            await HybridFileMutex.AcquireAsync(filePath, TimeSpan.FromSeconds(3)));

        mutex2.Should().NotBeNull();
        mutex2.Release();
    }

    [Fact]
    public async Task AcquireAsync_WithCancellation_ShouldRespect()
    {
        var filePath = GetTestFile("cancel.json");

        await using var mutex1 = await HybridFileMutex.AcquireAsync(filePath, TimeSpan.FromSeconds(5));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var act = async () => await HybridFileMutex.AcquireAsync(filePath, TimeSpan.FromSeconds(5), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Acquire_LockFileShouldBeCreated()
    {
        var filePath = GetTestFile("lockfile_create.json");

        var mutex = HybridFileMutex.Acquire(filePath, TimeSpan.FromSeconds(5));
        var lockFilePath = HybridFileMutex.GetLockFilePath(filePath);
        File.Exists(lockFilePath).Should().BeTrue();

        var content = await File.ReadAllTextAsync(lockFilePath);
        content.Should().Contain($"PID:{Environment.ProcessId}");

        mutex.Release();
    }

    [Fact]
    public void Release_LockFileShouldBeDeleted()
    {
        var filePath = GetTestFile("lockfile_delete.json");

        var mutex = HybridFileMutex.Acquire(filePath, TimeSpan.FromSeconds(5));
        var lockFilePath = HybridFileMutex.GetLockFilePath(filePath);
        File.Exists(lockFilePath).Should().BeTrue();

        mutex.Release();

        File.Exists(lockFilePath).Should().BeFalse();
    }

    [Fact]
    public void GetMutexName_ShouldReturnGlobalPrefix()
    {
        var filePath = GetTestFile("mutex_name.json");
        var name = HybridFileMutex.GetMutexName(filePath);

        name.Should().StartWith("Global\\McpHost_FileLock_");
    }

    [Fact]
    public void GetMutexName_SamePath_ShouldReturnSameName()
    {
        var filePath = GetTestFile("same_name.json");
        var name1 = HybridFileMutex.GetMutexName(filePath);
        var name2 = HybridFileMutex.GetMutexName(filePath);

        name1.Should().Be(name2);
    }

    [Fact]
    public void GetMutexName_DifferentPaths_ShouldReturnDifferentNames()
    {
        var name1 = HybridFileMutex.GetMutexName(GetTestFile("a.json"));
        var name2 = HybridFileMutex.GetMutexName(GetTestFile("b.json"));

        name1.Should().NotBe(name2);
    }

    [Fact]
    public async Task Acquire_Timeout_ShouldThrowTimeoutException()
    {
        var filePath = GetTestFile("timeout.json");

        var mutex1 = HybridFileMutex.Acquire(filePath, TimeSpan.FromSeconds(5));

        var act = async () => await Task.Run(() =>
            HybridFileMutex.Acquire(filePath, TimeSpan.FromMilliseconds(100)));

        await act.Should().ThrowAsync<TimeoutException>();

        mutex1.Release();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_ShouldNotThrow()
    {
        var filePath = GetTestFile("double_dispose.json");

        var mutex = await HybridFileMutex.AcquireAsync(filePath, TimeSpan.FromSeconds(5));
        await mutex.DisposeAsync();

        var act = async () => await mutex.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Release_CalledTwice_ShouldNotThrow()
    {
        var filePath = GetTestFile("double_release.json");

        var mutex = HybridFileMutex.Acquire(filePath, TimeSpan.FromSeconds(5));
        mutex.Release();

        var act = () => mutex.Release();
        act.Should().NotThrow();

        await Task.CompletedTask;
    }
}
