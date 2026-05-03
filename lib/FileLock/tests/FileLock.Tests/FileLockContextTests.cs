namespace FileLock.Tests;

public sealed class FileLockContextTests : IDisposable
{
    private readonly string _testDir;

    public FileLockContextTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"FileLockCtxTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        FileLockContext.EnforcementEnabled = true;
    }

    public void Dispose()
    {
        FileLockContext.EnforcementEnabled = true;
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private string GetTestFile(string name) => Path.Combine(_testDir, name);

    [Fact]
    public void EnterLock_ShouldAcquireMutex()
    {
        var filePath = GetTestFile("enter.json");
        File.WriteAllText(filePath, "{}");

        using var scope = FileLockContext.EnterLock(filePath);

        FileLockContext.IsFileLocked(filePath).Should().BeTrue();
        FileLockContext.HasAnyLock().Should().BeTrue();
    }

    [Fact]
    public void EnterLock_Dispose_ShouldReleaseMutex()
    {
        var filePath = GetTestFile("enter_dispose.json");
        File.WriteAllText(filePath, "{}");

        var scope = FileLockContext.EnterLock(filePath);
        scope.Dispose();

        FileLockContext.IsFileLocked(filePath).Should().BeFalse();
    }

    [Fact]
    public void EnterLock_AfterRelease_ShouldSucceed()
    {
        var filePath = GetTestFile("ctx_reacquire.json");
        File.WriteAllText(filePath, "{}");

        using (var scope1 = FileLockContext.EnterLock(filePath))
        {
            FileLockContext.IsFileLocked(filePath).Should().BeTrue();
        }

        using var scope2 = FileLockContext.EnterLock(filePath);
        FileLockContext.IsFileLocked(filePath).Should().BeTrue();
    }

    [Fact]
    public void DisableEnforcement_ShouldToggle()
    {
        FileLockContext.EnforcementEnabled.Should().BeTrue();

        using (FileLockContext.DisableEnforcement())
        {
            FileLockContext.EnforcementEnabled.Should().BeFalse();
        }

        FileLockContext.EnforcementEnabled.Should().BeTrue();
    }

    [Fact]
    public void DisableEnforcement_Nested_ShouldRestorePreviousValue()
    {
        FileLockContext.EnforcementEnabled = true;

        using (FileLockContext.DisableEnforcement())
        {
            FileLockContext.EnforcementEnabled.Should().BeFalse();

            using (FileLockContext.DisableEnforcement())
            {
                FileLockContext.EnforcementEnabled.Should().BeFalse();
            }

            FileLockContext.EnforcementEnabled.Should().BeFalse();
        }

        FileLockContext.EnforcementEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsFileLocked_UnknownFile_ShouldReturnFalse()
    {
        var filePath = GetTestFile("unknown.json");
        FileLockContext.IsFileLocked(filePath).Should().BeFalse();
    }

    [Fact]
    public void HasAnyLock_NoLocks_ShouldReturnFalse()
    {
        FileLockContext.HasAnyLock().Should().BeFalse();
    }
}
