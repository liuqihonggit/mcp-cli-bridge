namespace Benchmarks;

/// <summary>
/// 基准测试入口
/// 支持选择运行不同的基准测试套件
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        // 如果指定了参数，运行指定的基准测试
        if (args.Length > 0)
        {
            RunSpecificBenchmark(args[0]);
            return;
        }

        // 否则运行所有基准测试
        RunAllBenchmarks();
    }

    private static void RunSpecificBenchmark(string benchmarkName)
    {
        switch (benchmarkName.ToLowerInvariant())
        {
            case "reflection":
            case "invoker":
                BenchmarkRunner.Run<ReflectionOptimizationBenchmark>();
                BenchmarkRunner.Run<InvokerCacheBenchmark>();
                break;

            case "cache":
                BenchmarkRunner.Run<CachePerformanceBenchmark>();
                BenchmarkRunner.Run<CacheConcurrencyBenchmark>();
                BenchmarkRunner.Run<CacheEvictionBenchmark>();
                break;

            case "all":
                RunAllBenchmarks();
                break;

            default:
                Console.WriteLine($"Unknown benchmark: {benchmarkName}");
                Console.WriteLine("Available options: reflection, cache, all");
                break;
        }
    }

    private static void RunAllBenchmarks()
    {
        Console.WriteLine("=== Running Reflection Optimization Benchmarks ===");
        BenchmarkRunner.Run<ReflectionOptimizationBenchmark>();
        BenchmarkRunner.Run<InvokerCacheBenchmark>();

        Console.WriteLine("\n=== Running Cache Performance Benchmarks ===");
        BenchmarkRunner.Run<CachePerformanceBenchmark>();
        BenchmarkRunner.Run<CacheConcurrencyBenchmark>();
        BenchmarkRunner.Run<CacheEvictionBenchmark>();
    }
}
