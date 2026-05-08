namespace AstCli.E2E;

using System.Text.Json;

class Program
{
    private static readonly List<TestResult> Results = [];

    static int Main(string[] args)
    {
        Console.WriteLine("=== AstCli String Operations E2E Test ===");
        Console.WriteLine();

        var projectRoot = FindProjectRoot();
        var astCliPath = AstCliRunner.FindAstCliExecutable();
        var runner = new AstCliRunner(astCliPath);
        var demo = new CodeDemoHelper(projectRoot);

        Console.WriteLine($"Project Root: {projectRoot}");
        Console.WriteLine($"AstCli Path:  {astCliPath}");
        Console.WriteLine($"CodeDemo Dir: {demo.CodeDemoDir}");
        Console.WriteLine();

        Console.WriteLine("--- Step 1: Clean and copy CodeDemo ---");
        demo.CleanAndCopy();
        Console.WriteLine("CodeDemo ready.");
        Console.WriteLine();

        // ========================================
        // string_query
        // ========================================
        Console.WriteLine("--- Step 2: string_query tests ---");
        RunTest("string_query returns results with filter", () =>
        {
            var result = runner.Execute(new Dictionary<string, object>
            {
                ["command"] = "string_query",
                ["projectPath"] = demo.CodeDemoDir,
                ["filter"] = "memory_"
            });

            Assert(result.IsSuccess, $"string_query failed: {result}");
            var json = result.ParseJson();
            var totalCount = json.GetProperty("totalCount").GetInt32();
            Assert(totalCount > 0, $"totalCount should be > 0, got {totalCount}");
        });

        RunTest("string_query with prefix filter", () =>
        {
            var result = runner.Execute(new Dictionary<string, object>
            {
                ["command"] = "string_query",
                ["projectPath"] = demo.CodeDemoDir,
                ["prefix"] = "memory_"
            });

            Assert(result.IsSuccess, $"string_query failed: {result}");
            var json = result.ParseJson();
            var totalCount = json.GetProperty("totalCount").GetInt32();
            Assert(totalCount > 0, $"totalCount with prefix should be > 0, got {totalCount}");
        });

        RunTest("string_query specific file", () =>
        {
            var filePath = Path.Combine(demo.CodeDemoDir, "src", "Common", "Constants", "ConstantManager.cs");
            var result = runner.Execute(new Dictionary<string, object>
            {
                ["command"] = "string_query",
                ["projectPath"] = demo.CodeDemoDir,
                ["filePath"] = filePath,
                ["filter"] = "memory_"
            });

            Assert(result.IsSuccess, $"string_query failed: {result}");
            var json = result.ParseJson();
            var totalCount = json.GetProperty("totalCount").GetInt32();
            Assert(totalCount > 0, $"totalCount for specific file should be > 0, got {totalCount}");
        });

        Console.WriteLine();

        // ========================================
        // string_replace (literal)
        // ========================================
        Console.WriteLine("--- Step 3: string_replace tests ---");
        RunTest("string_replace memory_ -> men_ replaces strings", () =>
        {
            var beforeCount = demo.CountOccurrences("memory_");
            Assert(beforeCount > 0, "should have memory_ strings before replace");

            var result = runner.Execute(new Dictionary<string, object>
            {
                ["command"] = "string_replace",
                ["projectPath"] = demo.CodeDemoDir,
                ["pattern"] = "memory_",
                ["replacement"] = "men_",
                ["filter"] = "memory_"
            });

            Assert(result.IsSuccess, $"string_replace failed: {result}");
            var json = result.ParseJson();
            Assert(json.GetProperty("success").GetBoolean(), "success should be true");
            Assert(json.GetProperty("transformedCount").GetInt32() > 0, "transformedCount should be > 0");
            Assert(json.GetProperty("modifiedFiles").GetArrayLength() > 0, "modifiedFiles should not be empty");

            var afterCount = demo.CountOccurrences("memory_");
            Assert(afterCount < beforeCount, $"memory_ count should decrease: {beforeCount} -> {afterCount}");

            var menCount = demo.CountOccurrences("men_");
            Assert(menCount > 0, "men_ should exist after replace");
        });

        RunTest("string_replace: build solution", () =>
        {
            Assert(demo.BuildSolution(), "Solution should compile after string_replace");
        });

        RunTest("string_replace: unit tests can execute", () =>
        {
            var testResult = demo.RunAllUnitTests();
            Assert(testResult.TotalCount > 0, $"Unit tests should be discoverable, got Total={testResult.TotalCount}");
        });

        RunTest("string_replace: StringLiteral unit tests can execute", () =>
        {
            var testResult = demo.RunStringLiteralUnitTests();
            Assert(testResult.TotalCount > 0, $"StringLiteral tests should be discoverable, got Total={testResult.TotalCount}");
        });

        Console.WriteLine();

        // ========================================
        // string_prefix
        // ========================================
        Console.WriteLine("--- Step 4: Re-prepare CodeDemo for prefix tests ---");
        demo.CleanAndCopy();
        Console.WriteLine("CodeDemo reset.");
        Console.WriteLine();

        Console.WriteLine("--- Step 5: string_prefix tests ---");
        RunTest("string_prefix adds prefix to filtered strings", () =>
        {
            var beforeCount = demo.CountOccurrences("PREFIX_memory_");
            Assert(beforeCount == 0, "should not have PREFIX_ before prefix operation");

            var result = runner.Execute(new Dictionary<string, object>
            {
                ["command"] = "string_prefix",
                ["projectPath"] = demo.CodeDemoDir,
                ["insertText"] = "PREFIX_",
                ["filter"] = "memory_"
            });

            Assert(result.IsSuccess, $"string_prefix failed: {result}");
            var json = result.ParseJson();
            Assert(json.GetProperty("success").GetBoolean(), "success should be true");
            Assert(json.GetProperty("transformedCount").GetInt32() > 0, "transformedCount should be > 0");

            var afterCount = demo.CountOccurrences("PREFIX_memory_");
            Assert(afterCount > 0, $"PREFIX_memory_ should exist after prefix, got {afterCount}");
        });

        RunTest("string_prefix: build solution", () =>
        {
            Assert(demo.BuildSolution(), "Solution should compile after string_prefix");
        });

        RunTest("string_prefix: unit tests can execute", () =>
        {
            var testResult = demo.RunAllUnitTests();
            Assert(testResult.TotalCount > 0, $"Unit tests should be discoverable, got Total={testResult.TotalCount}");
        });

        Console.WriteLine();

        // ========================================
        // string_suffix
        // ========================================
        Console.WriteLine("--- Step 6: Re-prepare CodeDemo for suffix tests ---");
        demo.CleanAndCopy();
        Console.WriteLine("CodeDemo reset.");
        Console.WriteLine();

        Console.WriteLine("--- Step 7: string_suffix tests ---");
        RunTest("string_suffix adds suffix to filtered strings", () =>
        {
            var beforeCount = demo.CountOccurrences("memory__SUFFIX");
            Assert(beforeCount == 0, "should not have _SUFFIX before suffix operation");

            var result = runner.Execute(new Dictionary<string, object>
            {
                ["command"] = "string_suffix",
                ["projectPath"] = demo.CodeDemoDir,
                ["insertText"] = "_SUFFIX",
                ["filter"] = "memory_"
            });

            Assert(result.IsSuccess, $"string_suffix failed: {result}");
            var json = result.ParseJson();
            Assert(json.GetProperty("success").GetBoolean(), "success should be true");
            Assert(json.GetProperty("transformedCount").GetInt32() > 0, "transformedCount should be > 0");

            var afterCount = demo.CountOccurrences("memory__SUFFIX");
            Assert(afterCount > 0, $"memory__SUFFIX should exist after suffix, got {afterCount}");
        });

        RunTest("string_suffix: build solution", () =>
        {
            Assert(demo.BuildSolution(), "Solution should compile after string_suffix");
        });

        RunTest("string_suffix: unit tests can execute", () =>
        {
            var testResult = demo.RunAllUnitTests();
            Assert(testResult.TotalCount > 0, $"Unit tests should be discoverable, got Total={testResult.TotalCount}");
        });

        Console.WriteLine();

        // ========================================
        // string_insert
        // ========================================
        Console.WriteLine("--- Step 8: Re-prepare CodeDemo for insert tests ---");
        demo.CleanAndCopy();
        Console.WriteLine("CodeDemo reset.");
        Console.WriteLine();

        Console.WriteLine("--- Step 9: string_insert tests ---");
        RunTest("string_insert at position 0 equals prefix", () =>
        {
            var result = runner.Execute(new Dictionary<string, object>
            {
                ["command"] = "string_insert",
                ["projectPath"] = demo.CodeDemoDir,
                ["insertText"] = "INS_",
                ["position"] = 0,
                ["filter"] = "memory_"
            });

            Assert(result.IsSuccess, $"string_insert failed: {result}");
            var json = result.ParseJson();
            Assert(json.GetProperty("success").GetBoolean(), "success should be true");
            Assert(json.GetProperty("transformedCount").GetInt32() > 0, "transformedCount should be > 0");

            var afterCount = demo.CountOccurrences("INS_memory_");
            Assert(afterCount > 0, $"INS_memory_ should exist after insert at 0, got {afterCount}");
        });

        RunTest("string_insert: build solution", () =>
        {
            Assert(demo.BuildSolution(), "Solution should compile after string_insert");
        });

        RunTest("string_insert: unit tests can execute", () =>
        {
            var testResult = demo.RunAllUnitTests();
            Assert(testResult.TotalCount > 0, $"Unit tests should be discoverable, got Total={testResult.TotalCount}");
        });

        Console.WriteLine();

        // ========================================
        // dryRun
        // ========================================
        Console.WriteLine("--- Step 10: Re-prepare CodeDemo for dryRun test ---");
        demo.CleanAndCopy();
        Console.WriteLine("CodeDemo reset.");
        Console.WriteLine();

        Console.WriteLine("--- Step 11: dryRun test ---");
        RunTest("string_replace dryRun does not modify files", () =>
        {
            var beforeCount = demo.CountOccurrences("memory_");

            var result = runner.Execute(new Dictionary<string, object>
            {
                ["command"] = "string_replace",
                ["projectPath"] = demo.CodeDemoDir,
                ["pattern"] = "memory_",
                ["replacement"] = "men_",
                ["filter"] = "memory_",
                ["dryRun"] = true
            });

            Assert(result.IsSuccess, $"string_replace dryRun failed: {result}");
            var json = result.ParseJson();
            Assert(json.GetProperty("success").GetBoolean(), "success should be true");

            var afterCount = demo.CountOccurrences("memory_");
            Assert(afterCount == beforeCount,
                $"dryRun should not modify files: {beforeCount} -> {afterCount}");
        });

        Console.WriteLine();

        // ========================================
        // regex replace
        // ========================================
        Console.WriteLine("--- Step 12: Re-prepare CodeDemo for regex test ---");
        demo.CleanAndCopy();
        Console.WriteLine("CodeDemo reset.");
        Console.WriteLine();

        Console.WriteLine("--- Step 13: regex replace test ---");
        RunTest("string_replace with regex pattern", () =>
        {
            var beforeCount = demo.CountOccurrences("memory_");
            Assert(beforeCount > 0, "should have memory_ strings before replace");

            var result = runner.Execute(new Dictionary<string, object>
            {
                ["command"] = "string_replace",
                ["projectPath"] = demo.CodeDemoDir,
                ["pattern"] = "memory_(\\w+)",
                ["replacement"] = "men_$1",
                ["useRegex"] = true,
                ["filter"] = "memory_"
            });

            Assert(result.IsSuccess, $"string_replace regex failed: {result}");
            var json = result.ParseJson();
            Assert(json.GetProperty("success").GetBoolean(), "success should be true");
            Assert(json.GetProperty("transformedCount").GetInt32() > 0, "transformedCount should be > 0");

            var afterCount = demo.CountOccurrences("memory_");
            Assert(afterCount < beforeCount, $"memory_ count should decrease: {beforeCount} -> {afterCount}");

            var menCount = demo.CountOccurrences("men_");
            Assert(menCount > 0, "men_ should exist after regex replace");
        });

        RunTest("string_replace regex: build solution", () =>
        {
            Assert(demo.BuildSolution(), "Solution should compile after regex replace");
        });

        RunTest("string_replace regex: unit tests can execute", () =>
        {
            var testResult = demo.RunAllUnitTests();
            Assert(testResult.TotalCount > 0, $"Unit tests should be discoverable, got Total={testResult.TotalCount}");
        });

        RunTest("string_replace regex: StringLiteral unit tests can execute", () =>
        {
            var testResult = demo.RunStringLiteralUnitTests();
            Assert(testResult.TotalCount > 0, $"StringLiteral tests should be discoverable, got Total={testResult.TotalCount}");
        });

        Console.WriteLine();

        PrintSummary();

        return Results.Count(r => !r.Passed);
    }

    private static void RunTest(string name, Action action)
    {
        Console.Write($"  [{Results.Count + 1:D2}] {name} ... ");
        try
        {
            action();
            Results.Add(new TestResult(name, true, null));
            Console.WriteLine("PASSED");
        }
        catch (Exception ex)
        {
            Results.Add(new TestResult(name, false, ex.Message));
            Console.WriteLine("FAILED");
            Console.WriteLine($"       {ex.Message}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine("=== Test Summary ===");
        var passed = Results.Count(r => r.Passed);
        var failed = Results.Count(r => !r.Passed);
        Console.WriteLine($"Total: {Results.Count}  Passed: {passed}  Failed: {failed}");

        if (failed > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Failed tests:");
            foreach (var r in Results.Where(r => !r.Passed))
            {
                Console.WriteLine($"  - {r.Name}: {r.Error}");
            }
        }
    }

    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "McpHost.slnx")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new DirectoryNotFoundException("Cannot find project root (McpHost.slnx)");
    }
}

record TestResult(string Name, bool Passed, string? Error);
