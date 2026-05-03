namespace FileLock.Tests;

public sealed class HybridFileLockProviderTests : IDisposable
{
    private readonly string _testDir;
    private readonly HybridFileLockProvider _provider;

    public HybridFileLockProviderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"HybridLockTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _provider = new HybridFileLockProvider(new FileAccessOptions
        {
            LockTimeout = TimeSpan.FromSeconds(5),
            LockDirectory = Path.Combine(_testDir, "locks")
        });
    }

    public void Dispose()
    {
        _provider.Dispose();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private string GetTestFile(string name) => Path.Combine(_testDir, name);

    [Fact]
    public async Task AcquireBatchAsync_SingleFile_ShouldSucceed()
    {
        var filePath = GetTestFile("single.json");
        File.WriteAllText(filePath, "{}");

        var result = await _provider.AcquireBatchAsync([filePath], TimeSpan.FromSeconds(5));

        result.Success.Should().BeTrue();
        result.BatchLock.Should().NotBeNull();
        await result.BatchLock!.DisposeAsync();
    }

    [Fact]
    public async Task AcquireBatchAsync_MultipleFiles_ShouldSortAndAcquire()
    {
        var fileZ = GetTestFile("z.json");
        var fileA = GetTestFile("a.json");
        var fileM = GetTestFile("m.json");

        foreach (var f in new[] { fileZ, fileA, fileM })
            File.WriteAllText(f, "{}");

        var result = await _provider.AcquireBatchAsync([fileZ, fileA, fileM], TimeSpan.FromSeconds(5));

        result.Success.Should().BeTrue();
        var paths = result.BatchLock!.FilePaths;
        paths.Should().HaveCount(3);
        paths[0].Should().EndWith("a.json");
        paths[1].Should().EndWith("m.json");
        paths[2].Should().EndWith("z.json");

        await result.BatchLock.DisposeAsync();
    }

    [Fact]
    public async Task AcquireBatchAsync_DuplicatePaths_ShouldDeduplicate()
    {
        var filePath = GetTestFile("dup.json");
        File.WriteAllText(filePath, "{}");

        var result = await _provider.AcquireBatchAsync(
            [filePath, filePath, filePath], TimeSpan.FromSeconds(5));

        result.Success.Should().BeTrue();
        result.BatchLock!.FilePaths.Should().ContainSingle();
        await result.BatchLock.DisposeAsync();
    }

    [Fact]
    public async Task BatchLock_DisposeAsync_ShouldReleaseAndAllowReacquire()
    {
        var filePath = GetTestFile("release.json");
        File.WriteAllText(filePath, "{}");

        var result = await _provider.AcquireBatchAsync([filePath], TimeSpan.FromSeconds(5));
        await result.BatchLock!.DisposeAsync();

        result.BatchLock.IsDisposed.Should().BeTrue();

        var result2 = await _provider.AcquireBatchAsync([filePath], TimeSpan.FromSeconds(1));
        result2.Success.Should().BeTrue();
        await result2.BatchLock!.DisposeAsync();
    }

    [Fact]
    public async Task ConcurrentAccess_SameProcessTwoThreads_SecondShouldTimeout()
    {
        var filePath = GetTestFile("intra_process.json");
        File.WriteAllText(filePath, "{}");

        var result1 = await _provider.AcquireBatchAsync([filePath], TimeSpan.FromSeconds(5));
        result1.Success.Should().BeTrue();

        var secondAttemptTask = Task.Run(async () =>
        {
            return await _provider.AcquireBatchAsync([filePath], TimeSpan.FromMilliseconds(200));
        });

        var secondResult = await secondAttemptTask;
        secondResult.Success.Should().BeFalse();

        await result1.BatchLock!.DisposeAsync();
    }

    [Fact]
    public async Task ConcurrentAccess_AfterRelease_SecondShouldSucceed()
    {
        var filePath = GetTestFile("after_release.json");
        File.WriteAllText(filePath, "{}");

        var result1 = await _provider.AcquireBatchAsync([filePath], TimeSpan.FromSeconds(5));
        result1.Success.Should().BeTrue();
        await result1.BatchLock!.DisposeAsync();

        var result2 = await Task.Run(() =>
            _provider.AcquireBatchAsync([filePath], TimeSpan.FromSeconds(3)));

        result2.Success.Should().BeTrue();
        await result2.BatchLock!.DisposeAsync();
    }

    [Fact]
    public async Task ConcurrentAccess_MultipleFilesWithSorting_NoDeadlock()
    {
        var fileA = GetTestFile("sort_a.json");
        var fileB = GetTestFile("sort_b.json");
        var fileC = GetTestFile("sort_c.json");

        foreach (var f in new[] { fileA, fileB, fileC })
            File.WriteAllText(f, "{}");

        var t1 = Task.Run(async () =>
        {
            var r = await _provider.AcquireBatchAsync([fileB, fileC], TimeSpan.FromSeconds(5));
            r.Success.Should().BeTrue();
            await Task.Delay(100);
            await r.BatchLock!.DisposeAsync();
        });

        var t2 = Task.Run(async () =>
        {
            await Task.Delay(50);
            var r = await _provider.AcquireBatchAsync([fileA, fileB], TimeSpan.FromSeconds(5));
            r.Success.Should().BeTrue();
            await r.BatchLock!.DisposeAsync();
        });

        await Task.WhenAll(t1, t2);
    }

    [Fact]
    public async Task GetLockInfoAsync_WhenUnlocked_ShouldReturnFalse()
    {
        var filePath = GetTestFile("hybrid_unlocked.json");
        File.WriteAllText(filePath, "{}");

        var info = await _provider.GetLockInfoAsync(filePath);
        info.Should().NotBeNull();
        info!.IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task GetLockInfoAsync_WhenLocked_ShouldHaveLockFile()
    {
        var filePath = GetTestFile("hybrid_locked.json");
        File.WriteAllText(filePath, "{}");

        await using var batchLock = (await _provider.AcquireBatchAsync([filePath], TimeSpan.FromSeconds(5))).BatchLock;
        batchLock.Should().NotBeNull();

        var lockFiles = Directory.GetFiles(Path.Combine(_testDir, "locks"), "*.lock");
        lockFiles.Should().NotBeEmpty();

        var lockContent = await File.ReadAllTextAsync(lockFiles[0]);
        lockContent.Should().Contain($"PID:{Environment.ProcessId}");

        var info = await _provider.GetLockInfoAsync(filePath);
        info.Should().NotBeNull();
    }

    [Fact]
    public async Task AcquireBatchAsync_EmptyPaths_ShouldReturnError()
    {
        var result = await _provider.AcquireBatchAsync([], TimeSpan.FromSeconds(1));
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No files");
    }

    [Fact]
    public async Task AcquireBatchAsync_WithCancellation_ShouldRespectCancellation()
    {
        var fileA = GetTestFile("h_cancel_a.json");
        var fileB = GetTestFile("h_cancel_b.json");
        File.WriteAllText(fileA, "{}");
        File.WriteAllText(fileB, "{}");

        var result1 = await _provider.AcquireBatchAsync([fileA], TimeSpan.FromSeconds(5));
        result1.Success.Should().BeTrue();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        try
        {
            await _provider.AcquireBatchAsync([fileA, fileB], TimeSpan.FromSeconds(5), cts.Token);
            throw new Xunit.Sdk.XunitException("Expected cancellation");
        }
        catch (OperationCanceledException)
        {
        }

        await result1.BatchLock!.DisposeAsync();

        var result3 = await _provider.AcquireBatchAsync([fileB], TimeSpan.FromSeconds(1));
        result3.Success.Should().BeTrue();
        await result3.BatchLock!.DisposeAsync();
    }
}
