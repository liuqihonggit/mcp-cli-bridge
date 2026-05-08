namespace AstCli.E2E;

using System.Diagnostics;

public sealed class CodeDemoHelper
{
    private readonly string _projectRoot;
    private readonly string _codeDemoDir;

    public string CodeDemoDir => _codeDemoDir;

    public CodeDemoHelper(string projectRoot)
    {
        _projectRoot = projectRoot;
        _codeDemoDir = Path.Combine(projectRoot, "publish", "CodeDemo");
    }

    public void CleanAndCopy()
    {
        if (Directory.Exists(_codeDemoDir))
            Directory.Delete(_codeDemoDir, true);

        Directory.CreateDirectory(_codeDemoDir);

        CopyDirectory(
            Path.Combine(_projectRoot, "src"),
            Path.Combine(_codeDemoDir, "src"),
            ["bin", "obj"]);

        CopyDirectory(
            Path.Combine(_projectRoot, "tests"),
            Path.Combine(_codeDemoDir, "tests"),
            ["bin", "obj", "AstCliE2E"]);

        CopyRootFiles();
    }

    private void CopyRootFiles()
    {
        var files = new[] { "McpHost.slnx", "nuget.config", "Directory.Build.props" };
        foreach (var file in files)
        {
            var src = Path.Combine(_projectRoot, file);
            if (File.Exists(src))
                File.Copy(src, Path.Combine(_codeDemoDir, file), true);
        }

        var libDir = Path.Combine(_projectRoot, "lib");
        if (Directory.Exists(libDir))
            CopyDirectory(libDir, Path.Combine(_codeDemoDir, "lib"), ["bin", "obj"]);
    }

    public bool BuildProject(string relativeProjectPath)
    {
        var fullPath = Path.Combine(_codeDemoDir, relativeProjectPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Project not found: {fullPath}");

        return RunDotnet($"build \"{fullPath}\" -c Release");
    }

    public TestRunResult RunUnitTests(string relativeProjectPath, string? filter = null)
    {
        var fullPath = Path.Combine(_codeDemoDir, relativeProjectPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Project not found: {fullPath}");

        var args = $"test \"{fullPath}\" -c Release";
        if (filter != null)
            args += $" --filter \"{filter}\"";

        return RunDotnetTest(args);
    }

    public TestRunResult RunAllUnitTests()
    {
        return RunUnitTests("tests\\UnitTests\\MyMemoryServer.UnitTests.csproj");
    }

    public TestRunResult RunStringLiteralUnitTests()
    {
        return RunUnitTests("tests\\UnitTests\\MyMemoryServer.UnitTests.csproj", "StringLiteral");
    }

    public bool BuildSolution()
    {
        var ok = BuildProject("src\\Common.Contracts\\Common.Contracts.csproj");
        if (!ok) return false;
        ok = BuildProject("src\\Common\\Common.csproj");
        if (!ok) return false;
        ok = BuildProject("src\\McpHost\\McpHost.csproj");
        if (!ok) return false;
        ok = BuildProject("src\\Plugins\\MemoryCli\\MemoryCli.csproj");
        if (!ok) return false;
        ok = BuildProject("src\\Plugins\\FileReaderCli\\FileReaderCli.csproj");
        if (!ok) return false;
        ok = BuildProject("src\\Plugins\\AstCli\\AstCli.csproj");
        if (!ok) return false;
        ok = BuildProject("tests\\UnitTests\\MyMemoryServer.UnitTests.csproj");
        if (!ok) return false;
        ok = BuildProject("tests\\E2E\\MyMemoryServer.E2E.csproj");
        return ok;
    }

    private bool RunDotnet(string arguments)
    {
        using var process = new Process();
        process.StartInfo.FileName = "dotnet";
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        process.Start();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return process.ExitCode == 0;
    }

    private static TestRunResult RunDotnetTest(string arguments)
    {
        using var process = new Process();
        process.StartInfo.FileName = "dotnet";
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var passed = process.ExitCode == 0;

        var totalMatch = System.Text.RegularExpressions.Regex.Match(output, @"Total\s+:\s*(\d+)");
        if (!totalMatch.Success)
            totalMatch = System.Text.RegularExpressions.Regex.Match(output, @"总计\s*:\s*(\d+)");

        var failedMatch = System.Text.RegularExpressions.Regex.Match(output, @"Failed\s+:\s*(\d+)");
        if (!failedMatch.Success)
            failedMatch = System.Text.RegularExpressions.Regex.Match(output, @"失败\s*:\s*(\d+)");

        var passedMatch = System.Text.RegularExpressions.Regex.Match(output, @"Passed\s+:\s*(\d+)");
        if (!passedMatch.Success)
            passedMatch = System.Text.RegularExpressions.Regex.Match(output, @"已通过\s*:\s*(\d+)");

        return new TestRunResult(
            passed,
            passedMatch.Success ? int.Parse(passedMatch.Groups[1].Value) : 0,
            failedMatch.Success ? int.Parse(failedMatch.Groups[1].Value) : 0,
            totalMatch.Success ? int.Parse(totalMatch.Groups[1].Value) : 0,
            output);
    }

    public int CountOccurrences(string pattern, string? subDir = null)
    {
        var searchDir = subDir != null
            ? Path.Combine(_codeDemoDir, subDir)
            : _codeDemoDir;

        if (!Directory.Exists(searchDir))
            return 0;

        var count = 0;
        foreach (var file in Directory.GetFiles(searchDir, "*.cs", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            var idx = 0;
            while ((idx = content.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += pattern.Length;
            }
        }
        return count;
    }

    private static void CopyDirectory(string sourceDir, string destDir, string[] excludeDirs)
    {
        if (!Directory.Exists(sourceDir))
            return;

        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            if (excludeDirs.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                continue;

            CopyDirectory(dir, Path.Combine(destDir, dirName), excludeDirs);
        }
    }
}

public record TestRunResult(bool Passed, int PassedCount, int FailedCount, int TotalCount, string Output)
{
    public override string ToString() => $"Passed={PassedCount} Failed={FailedCount} Total={TotalCount}";
}
