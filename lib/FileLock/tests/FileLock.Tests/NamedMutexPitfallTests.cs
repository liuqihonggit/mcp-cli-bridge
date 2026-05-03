namespace FileLock.Tests;

public sealed class NamedMutexPitfallTests
{
    private static string GetUniqueMutexName() => $"Global\\McpHost_Test_{Guid.NewGuid():N}";

    [Fact]
    public async Task PureNamedMutex_SameThread_CanAcquireRecursively()
    {
        var mutexName = GetUniqueMutexName();

        using var mutex = new Mutex(initiallyOwned: false, mutexName);

        var acquired1 = mutex.WaitOne(TimeSpan.FromSeconds(5));
        acquired1.Should().BeTrue("first acquire should succeed");

        var acquired2 = mutex.WaitOne(TimeSpan.FromMilliseconds(100));
        acquired2.Should().BeTrue(
            "same thread can recursively acquire the same Mutex - this is the pitfall!");

        mutex.ReleaseMutex();
        mutex.ReleaseMutex();

        await Task.CompletedTask;
    }

    [Fact]
    public async Task PureNamedMutex_ThreadPoolReuse_CanBreakMutualExclusion()
    {
        var mutexName = GetUniqueMutexName();
        var acquiredBySecondTask = false;

        using var mutex = new Mutex(initiallyOwned: false, mutexName);
        mutex.WaitOne(TimeSpan.FromSeconds(5));

        var task1Thread = Thread.CurrentThread.ManagedThreadId;

        var task2 = Task.Run(() =>
        {
            var task2Thread = Thread.CurrentThread.ManagedThreadId;

            if (task2Thread == task1Thread)
            {
                try
                {
                    var acquired = mutex.WaitOne(TimeSpan.FromMilliseconds(100));
                    if (acquired)
                    {
                        acquiredBySecondTask = true;
                        mutex.ReleaseMutex();
                    }
                }
                catch
                {
                }
            }
        });

        await task2;

        mutex.ReleaseMutex();

        if (acquiredBySecondTask)
        {
        }
    }

    [Fact]
    public async Task HybridFileMutex_SameProcessTwoTasks_SecondMustWait()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"MutexPitfall_Hybrid_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        try
        {
            HybridFileMutex.Configure(
                Path.Combine(testDir, "locks"),
                TimeSpan.FromSeconds(30));

            var filePath = Path.Combine(testDir, "test.json");
            var secondAcquired = false;

            var mutex1 = HybridFileMutex.Acquire(filePath, TimeSpan.FromSeconds(5));

            var task2 = Task.Run(() =>
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

            await task2;
            secondAcquired.Should().BeFalse(
                "HybridFileMutex must block second concurrent task in same process");
            mutex1.Release();
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { }
        }
    }

    [Fact]
    public async Task HybridFileMutex_ThreadPoolStressTest_NoConcurrentAccess()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"MutexPitfall_Stress_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        try
        {
            HybridFileMutex.Configure(
                Path.Combine(testDir, "locks"),
                TimeSpan.FromSeconds(30));

            var filePath = Path.Combine(testDir, "stress.json");
            var concurrentCount = 0;
            var maxConcurrentCount = 0;
            var lockObj = new object();
            var tasks = new List<Task>();
            var successCount = 0;

            for (var i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await using var mutex = await HybridFileMutex.AcquireAsync(
                            filePath, TimeSpan.FromSeconds(10));

                        var current = Interlocked.Increment(ref concurrentCount);
                        lock (lockObj)
                        {
                            if (current > maxConcurrentCount)
                                maxConcurrentCount = current;
                        }

                        await Task.Delay(50);

                        Interlocked.Decrement(ref concurrentCount);
                        Interlocked.Increment(ref successCount);
                    }
                    catch (TimeoutException)
                    {
                    }
                }));
            }

            await Task.WhenAll(tasks);

            maxConcurrentCount.Should().Be(1,
                "only one task should hold the lock at any time");
            successCount.Should().Be(20,
                "all tasks should eventually acquire and release the lock");
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { }
        }
    }

    [Fact]
    public async Task HybridFileMutex_MultipleFiles_DifferentLocksCanBeHeldConcurrently()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"MutexPitfall_MultiFile_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        try
        {
            HybridFileMutex.Configure(
                Path.Combine(testDir, "locks"),
                TimeSpan.FromSeconds(30));

            var fileA = Path.Combine(testDir, "a.json");
            var fileB = Path.Combine(testDir, "b.json");

            await using var mutexA = await HybridFileMutex.AcquireAsync(fileA, TimeSpan.FromSeconds(5));
            await using var mutexB = await HybridFileMutex.AcquireAsync(fileB, TimeSpan.FromSeconds(5));

            var fileATask = Task.Run(async () =>
            {
                try
                {
                    await HybridFileMutex.AcquireAsync(fileA, TimeSpan.FromMilliseconds(200));
                    return false;
                }
                catch (TimeoutException)
                {
                    return true;
                }
            });

            var fileBTask = Task.Run(async () =>
            {
                try
                {
                    await HybridFileMutex.AcquireAsync(fileB, TimeSpan.FromMilliseconds(200));
                    return false;
                }
                catch (TimeoutException)
                {
                    return true;
                }
            });

            var aResult = await fileATask;
            var bResult = await fileBTask;

            aResult.Should().BeTrue("file A is locked, second attempt should timeout");
            bResult.Should().BeTrue("file B is locked, second attempt should timeout");
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { }
        }
    }
}
