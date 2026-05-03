using System.Text.Json.Serialization.Metadata;

namespace FileLock.Tests;

public sealed class FileOperationHelperTests : IDisposable
{
    private readonly string _testDir;
    private static readonly JsonTypeInfo<TestData> TestDataTypeInfo = TestDataContext.Default.TestData;

    public FileOperationHelperTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"FileOpTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private string GetTestFile(string name) => Path.Combine(_testDir, name);

    [Fact]
    public async Task WriteJsonAsync_ShouldWriteFile()
    {
        var filePath = GetTestFile("write.json");
        var data = new TestData { Id = 1, Name = "test" };

        await FileOperationHelper.WriteJsonAsync(filePath, data, TestDataTypeInfo);

        File.Exists(filePath).Should().BeTrue();
        var read = await FileOperationHelper.ReadJsonAsync(filePath, TestDataTypeInfo);
        read.Should().NotBeNull();
        read!.Id.Should().Be(1);
        read.Name.Should().Be("test");
    }

    [Fact]
    public async Task ReadJsonAsync_NonExistentFile_ShouldReturnNull()
    {
        var filePath = GetTestFile("nonexistent.json");
        var result = await FileOperationHelper.ReadJsonAsync(filePath, TestDataTypeInfo);
        result.Should().BeNull();
    }

    [Fact]
    public async Task AppendJsonLineAsync_ShouldPreserveExistingContent()
    {
        var filePath = GetTestFile("append.jsonl");

        await FileOperationHelper.AppendJsonLineAsync(filePath, new TestData { Id = 1 }, TestDataTypeInfo);
        await FileOperationHelper.AppendJsonLineAsync(filePath, new TestData { Id = 2 }, TestDataTypeInfo);
        await FileOperationHelper.AppendJsonLineAsync(filePath, new TestData { Id = 3 }, TestDataTypeInfo);

        var lines = await FileOperationHelper.ReadJsonLinesAsync(filePath, TestDataTypeInfo);
        lines.Should().HaveCount(3);
        lines[0].Id.Should().Be(1);
        lines[1].Id.Should().Be(2);
        lines[2].Id.Should().Be(3);
    }

    [Fact]
    public async Task SaveJsonLinesAsync_ShouldOverwriteFile()
    {
        var filePath = GetTestFile("save_lines.jsonl");

        await FileOperationHelper.AppendJsonLineAsync(filePath, new TestData { Id = 99 }, TestDataTypeInfo);

        var items = new List<TestData>
        {
            new() { Id = 1 },
            new() { Id = 2 }
        };

        await FileOperationHelper.SaveJsonLinesAsync(filePath, items, TestDataTypeInfo);

        var lines = await FileOperationHelper.ReadJsonLinesAsync(filePath, TestDataTypeInfo);
        lines.Should().HaveCount(2);
        lines[0].Id.Should().Be(1);
        lines[1].Id.Should().Be(2);
    }

    [Fact]
    public async Task ReadJsonLinesAsync_EmptyFile_ShouldReturnEmptyList()
    {
        var filePath = GetTestFile("empty.jsonl");
        File.WriteAllText(filePath, "");

        var lines = await FileOperationHelper.ReadJsonLinesAsync(filePath, TestDataTypeInfo);
        lines.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadJsonLinesAsync_NonExistentFile_ShouldReturnEmptyList()
    {
        var filePath = GetTestFile("nonexistent.jsonl");
        var lines = await FileOperationHelper.ReadJsonLinesAsync(filePath, TestDataTypeInfo);
        lines.Should().BeEmpty();
    }

    [Fact]
    public async Task WriteTextAsync_ShouldWriteTextFile()
    {
        var filePath = GetTestFile("text.txt");
        await FileOperationHelper.WriteTextAsync(filePath, "hello world");

        File.Exists(filePath).Should().BeTrue();
        var content = await FileOperationHelper.ReadTextAsync(filePath);
        content.Should().Be("hello world");
    }

    [Fact]
    public async Task ReadTextAsync_NonExistentFile_ShouldReturnNull()
    {
        var filePath = GetTestFile("no_text.txt");
        var content = await FileOperationHelper.ReadTextAsync(filePath);
        content.Should().BeNull();
    }

    [Fact]
    public async Task SafeMoveAsync_ShouldMoveFile()
    {
        var src = GetTestFile("move_src.txt");
        var dst = GetTestFile("move_dst.txt");

        await File.WriteAllTextAsync(src, "data");
        var result = await FileOperationHelper.SafeMoveAsync(src, dst);

        result.Should().BeTrue();
        File.Exists(src).Should().BeFalse();
        File.Exists(dst).Should().BeTrue();
        (await File.ReadAllTextAsync(dst)).Should().Be("data");
    }

    [Fact]
    public async Task SafeMoveAsync_SourceNotExists_ShouldReturnFalse()
    {
        var src = GetTestFile("move_no_src.txt");
        var dst = GetTestFile("move_no_src_dst.txt");

        var result = await FileOperationHelper.SafeMoveAsync(src, dst);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CopyAsync_ShouldCopyFile()
    {
        var src = GetTestFile("copy_src.txt");
        var dst = GetTestFile("copy_dst.txt");

        await File.WriteAllTextAsync(src, "copy data");
        var result = await FileOperationHelper.CopyAsync(src, dst);

        result.Should().BeTrue();
        File.Exists(src).Should().BeTrue();
        File.Exists(dst).Should().BeTrue();
        (await File.ReadAllTextAsync(dst)).Should().Be("copy data");
    }

    [Fact]
    public void SafeDelete_ShouldDeleteFile()
    {
        var filePath = GetTestFile("delete_me.txt");
        File.WriteAllText(filePath, "temp");

        var result = FileOperationHelper.SafeDelete(filePath);
        result.Should().BeTrue();
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public void SafeDelete_NonExistentFile_ShouldReturnFalse()
    {
        var filePath = GetTestFile("no_such_file.txt");
        var result = FileOperationHelper.SafeDelete(filePath);
        result.Should().BeFalse();
    }

    [Fact]
    public void GetFileSize_ExistingFile_ShouldReturnSize()
    {
        var filePath = GetTestFile("size.txt");
        File.WriteAllText(filePath, "12345");

        var size = FileOperationHelper.GetFileSize(filePath);
        size.Should().Be(5);
    }

    [Fact]
    public void GetFileSize_NonExistentFile_ShouldReturnZero()
    {
        var filePath = GetTestFile("no_size.txt");
        var size = FileOperationHelper.GetFileSize(filePath);
        size.Should().Be(0);
    }

    [Fact]
    public void EnsureDirectory_ShouldCreateDirectoryChain()
    {
        var filePath = Path.Combine(_testDir, "sub", "deep", "file.json");
        FileOperationHelper.EnsureDirectory(filePath);

        Directory.Exists(Path.GetDirectoryName(filePath)).Should().BeTrue();
    }

    [Fact]
    public void GenerateTimestampedFileName_ShouldIncludeTimestamp()
    {
        var name = FileOperationHelper.GenerateTimestampedFileName("log", ".json");
        name.Should().StartWith("log_");
        name.Should().EndWith(".json");
        name.Length.Should().BeGreaterThan("log_.json".Length);
    }

    [Fact]
    public async Task IsFileLocked_OpenFile_ShouldReturnTrue()
    {
        var filePath = GetTestFile("islocked.txt");
        await File.WriteAllTextAsync(filePath, "content");

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
        FileOperationHelper.IsFileLocked(filePath).Should().BeTrue();
    }

    [Fact]
    public async Task IsFileLocked_ClosedFile_ShouldReturnFalse()
    {
        var filePath = GetTestFile("islocked_false.txt");
        await File.WriteAllTextAsync(filePath, "content");

        FileOperationHelper.IsFileLocked(filePath).Should().BeFalse();
    }

    [Fact]
    public void IsFileLocked_NonExistentFile_ShouldReturnFalse()
    {
        var filePath = GetTestFile("no_locked.txt");
        FileOperationHelper.IsFileLocked(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task WaitForFileUnlockAsync_WhenUnlocked_ShouldReturnTrue()
    {
        var filePath = GetTestFile("wait_unlock.txt");
        await File.WriteAllTextAsync(filePath, "content");

        var result = await FileOperationHelper.WaitForFileUnlockAsync(filePath, TimeSpan.FromSeconds(1));
        result.Should().BeTrue();
    }

    [Fact]
    public async Task WriteJsonAsync_NoOrphanTempFiles()
    {
        var filePath = GetTestFile("no_orphan.json");
        var dir = Path.GetDirectoryName(filePath)!;

        var filesBefore = Directory.GetFiles(dir, "*.tmp*").Length;

        var data = new TestData { Id = 42, Name = "cow-test" };
        await FileOperationHelper.WriteJsonAsync(filePath, data, TestDataTypeInfo);

        var filesAfter = Directory.GetFiles(dir, "*.tmp*").Length;
        filesAfter.Should().Be(filesBefore);

        File.Exists(filePath).Should().BeTrue();
        var read = await FileOperationHelper.ReadJsonAsync(filePath, TestDataTypeInfo);
        read!.Id.Should().Be(42);
    }

    [Fact]
    public async Task AppendJsonLineAsync_LargeFile_ShouldPreserveAllContent()
    {
        var filePath = GetTestFile("large_append.jsonl");
        var expectedCount = 100;

        for (int i = 0; i < expectedCount; i++)
        {
            await FileOperationHelper.AppendJsonLineAsync(
                filePath, new TestData { Id = i }, TestDataTypeInfo);
        }

        var lines = await FileOperationHelper.ReadJsonLinesAsync(filePath, TestDataTypeInfo);
        lines.Should().HaveCount(expectedCount);

        for (int i = 0; i < expectedCount; i++)
        {
            lines[i].Id.Should().Be(i);
        }
    }
}

internal sealed class TestData
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

[JsonSerializable(typeof(TestData))]
internal sealed partial class TestDataContext : JsonSerializerContext
{
}
