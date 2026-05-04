global using static Common.Constants.ConstantManager;
using AsyncFileLock;

namespace McpHost.Services;

public sealed class PackageManager : IPackageManager
{
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        nameof(McpHost),
        DirectoryNames.Cache
    );

    private static readonly string ToolsDirectory = Path.Combine(CacheDirectory, DirectoryNames.Tools);
    private readonly ILogger _logger;

    public PackageManager(ILogger logger)
    {
        _logger = logger;
        EnsureDirectories();
    }

    private static void EnsureDirectories()
    {
        if (!Directory.Exists(CacheDirectory))
            Directory.CreateDirectory(CacheDirectory);
        if (!Directory.Exists(ToolsDirectory))
            Directory.CreateDirectory(ToolsDirectory);
    }

    public string GetToolsDirectory() => ToolsDirectory;

    public string? GetExecutablePath(string toolName)
    {
        var platform = GetPlatformIdentifier();

        var processDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName);
        if (!string.IsNullOrEmpty(processDir))
        {
            var pluginSubDirPath = Path.Combine(processDir, DirectoryNames.Plugins, toolName, $"{toolName}{FileExtensions.Exe}");
            if (File.Exists(pluginSubDirPath))
                return pluginSubDirPath;

            var pluginsDirPath = Path.Combine(processDir, DirectoryNames.Plugins, $"{toolName}{FileExtensions.Exe}");
            if (File.Exists(pluginsDirPath))
                return pluginsDirPath;

            var processDirPath = Path.Combine(processDir, $"{toolName}{FileExtensions.Exe}");
            if (File.Exists(processDirPath))
                return processDirPath;
        }

        var arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        var searchPaths = new[]
        {
            Path.Combine(ToolsDirectory, DirectoryNames.Plugins, toolName, toolName),
            Path.Combine(ToolsDirectory, DirectoryNames.Plugins, toolName),
            Path.Combine(ToolsDirectory, DirectoryNames.Cli, $"{platform}-{arch}", toolName),
            Path.Combine(ToolsDirectory, DirectoryNames.Cli, platform, toolName),
            Path.Combine(ToolsDirectory, DirectoryNames.Cli, toolName),
            Path.Combine(ToolsDirectory, toolName),
        };

        foreach (var path in searchPaths)
        {
            var fullPath = path;
            if (platform == Platforms.Windows && !fullPath.EndsWith(FileExtensions.Exe))
                fullPath += FileExtensions.Exe;

            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    public IReadOnlyList<string> DiscoverAvailablePlugins()
    {
        var plugins = new List<string>();

        var processDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName);
        if (!string.IsNullOrEmpty(processDir))
        {
            var pluginsDir = Path.Combine(processDir, DirectoryNames.Plugins);
            if (Directory.Exists(pluginsDir))
            {
                foreach (var subDir in Directory.GetDirectories(pluginsDir))
                {
                    var exeFiles = Directory.GetFiles(subDir, "*.exe");
                    foreach (var exeFile in exeFiles)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(exeFile);
                        if (!plugins.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                        {
                            plugins.Add(fileName);
                            _logger.Info($"Discovered CLI plugin: {fileName}");
                        }
                    }
                }

                var rootExeFiles = Directory.GetFiles(pluginsDir, "*.exe");
                foreach (var exeFile in rootExeFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(exeFile);
                    if (!plugins.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                    {
                        plugins.Add(fileName);
                        _logger.Info($"Discovered CLI plugin: {fileName}");
                    }
                }
            }

            var exeFilesInRoot = Directory.GetFiles(processDir, "*.exe");
            foreach (var exeFile in exeFilesInRoot)
            {
                var fileName = Path.GetFileNameWithoutExtension(exeFile);
                if (fileName.Equals("McpHost", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!plugins.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                {
                    plugins.Add(fileName);
                    _logger.Info($"Discovered CLI plugin: {fileName}");
                }
            }
        }

        return plugins;
    }

    public async Task<bool> DownloadPackageAsync(string packageName, string? version = null)
    {
        try
        {
            var npmPath = FindNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                _logger.Error("npm not found in PATH");
                return false;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), $"mcp-cli-bridge-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var versionArg = version != null ? $"@{version}" : "";
                var result = await RunProcessAsync(npmPath, $"pack {packageName}{versionArg} --pack-destination \"{tempDir}\"");

                if (!result.Success)
                {
                    _logger.Error($"Failed to download package: {result.Error}");
                    return false;
                }

                var tarball = Directory.GetFiles(tempDir, $"*{FileExtensions.Tgz}").FirstOrDefault();
                if (tarball == null)
                {
                    _logger.Error("Package tarball not found");
                    return false;
                }

                await ExtractTarballAsync(tarball, ToolsDirectory);
                return true;
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error downloading package");
            return false;
        }
    }

    private static string? FindNpmPath()
    {
        var npmCmd = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? FileNames.NpmCmd : FileNames.Npm;

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;

        foreach (var path in pathEnv.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(path, npmCmd);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    private static async Task<OperationResult> RunProcessAsync(string command, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();

        var tcs = new TaskCompletionSource<bool>();
        process.EnableRaisingEvents = true;
        process.Exited += (s, e) => tcs.TrySetResult(true);

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await tcs.Task;
        stopwatch.Stop();

        return new OperationResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            Message = await stdoutTask,
            Error = await stderrTask,
            ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds
        };
    }

    private static async Task ExtractTarballAsync(string tarballPath, string destinationPath)
    {
        var lockResult = await FileLockService.AcquireAsync(tarballPath, TimeSpan.FromSeconds(5));
        if (!lockResult.Success || lockResult.Lock == null)
        {
            throw new TimeoutException($"Failed to acquire lock for tarball: {tarballPath}");
        }

        await using (var batchLock = lockResult.Lock)
        {
#pragma warning disable MCP001
            using var stream = File.OpenRead(tarballPath);
#pragma warning restore MCP001
            using var gzip = new GZipStream(stream, CompressionMode.Decompress);
            using var tar = new TarReader(gzip);

            while (tar.GetNextEntry() is { } entry)
            {
                if (entry.EntryType == TarEntryType.RegularFile)
                {
                    var relativePath = entry.Name;
                    if (relativePath.StartsWith("package/"))
                        relativePath = relativePath[8..];

                    var targetPath = Path.Combine(destinationPath, relativePath);
                    var targetDir = Path.GetDirectoryName(targetPath);

                    if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);

                    entry.ExtractToFile(targetPath, overwrite: true);
                }
            }
        }
    }

    private static string GetPlatformIdentifier()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Platforms.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return Platforms.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Platforms.OSX;
        return Platforms.Unknown;
    }
}
