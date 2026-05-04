using Common.Results;

namespace McpHost.PluginManager;

public sealed class CliToolProvider : IToolProvider, IAsyncDisposable, IDisposable
{
    private readonly ILogger _logger;
    private readonly IPackageManager _packageManager;
    private readonly IProcessPoolManager _processPoolManager;
    private readonly CliProviderConfiguration _configuration;
    private readonly Dictionary<string, CliToolMetadata> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly ProcessPoolOptions _poolOptions;
    private readonly TimeSpan _defaultTimeout;
    private readonly string? _executablePath;
    private readonly string _cliCommand;
    private PluginDescriptor? _pluginMetadata;
    private bool _commandsLoaded;
    private bool _disposed;

    public string ProviderName => _configuration.Name;

    public CliToolProvider(
        ILogger logger,
        IPackageManager packageManager,
        IProcessPoolManager processPoolManager,
        CliProviderConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _packageManager = packageManager ?? throw new ArgumentNullException(nameof(packageManager));
        _processPoolManager = processPoolManager ?? throw new ArgumentNullException(nameof(processPoolManager));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        _defaultTimeout = configuration.GetTimeout();
        _cliCommand = configuration.CliCommand ?? configuration.Name;
        _executablePath = configuration.ExecutablePath;

        _poolOptions = ProcessPoolOptions.FromConfiguration(
            configuration.ProcessPoolSize,
            configuration.Timeout);

        if (configuration.Tools.Count > 0)
        {
            LoadToolsFromConfiguration(configuration);
        }
    }

    public async Task<bool> DiscoverToolsAsync()
    {
        var executablePath = ResolveExecutablePath();
        if (string.IsNullOrEmpty(executablePath))
        {
            _logger.Log(LogLevel.Error, $"{_cliCommand} executable not found, cannot discover tools");
            return false;
        }

        try
        {
            var args = BuildListToolsArguments();
            var result = await ExecuteCliRawAsync(executablePath, args, _defaultTimeout);

            if (!result.Success)
            {
                _logger.Log(LogLevel.Error, $"Failed to discover tools from {_cliCommand}: {result.Error}");
                return false;
            }

            if (!ParsePluginDescriptor(result.Message))
            {
                return false;
            }

            await LoadCommandsAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, ex, $"Error discovering tools from {_cliCommand}");
            return false;
        }
    }

    public IReadOnlyList<IToolMetadata> GetAvailableTools()
    {
        return _tools.Values.Cast<IToolMetadata>().ToList().AsReadOnly();
    }

    public async Task<OperationResult> ExecuteAsync(
        string toolName,
        IReadOnlyDictionary<string, JsonElement> parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return CreateErrorResult("Tool name cannot be empty");
        }

        var executablePath = ResolveExecutablePath();
        if (string.IsNullOrEmpty(executablePath))
        {
            _logger.Log(LogLevel.Error, $"{_cliCommand} executable not found");
            return CreateErrorResult($"{_cliCommand} executable not found");
        }

        var timeout = _tools.TryGetValue(toolName, out var metadata) && metadata.DefaultTimeout > 0
            ? TimeSpan.FromMilliseconds(metadata.DefaultTimeout)
            : _defaultTimeout;

        return await ExecuteCliAsync(executablePath, parameters, timeout, cancellationToken);
    }

    public bool TryGetTool(string toolName, out CliToolMetadata? metadata)
    {
        return _tools.TryGetValue(toolName, out metadata);
    }

    #region Tool Discovery

    private bool ParsePluginDescriptor(string output)
    {
        try
        {
            var response = JsonSerializer.Deserialize(output, CommonJsonContext.Default.OperationResultPluginDescriptor);
            if (response is null || !response.Success)
            {
                _logger.Log(LogLevel.Warn, $"CLI returned unsuccessful response: {response?.Message ?? "null"}");
                return false;
            }

            if (response.Data is null)
            {
                _logger.Log(LogLevel.Warn, $"{_cliCommand} reported no plugin descriptor");
                return false;
            }

            _pluginMetadata = response.Data;
            _logger.Log(LogLevel.Info,
                $"Discovered plugin: {_pluginMetadata.Name} ({_pluginMetadata.Description}) with {_pluginMetadata.CommandCount} commands");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, ex, $"Failed to parse plugin descriptor from {_cliCommand}");
            return false;
        }
    }

    private async Task LoadCommandsAsync()
    {
        var executablePath = ResolveExecutablePath();
        if (string.IsNullOrEmpty(executablePath)) return;

        try
        {
            var args = BuildListCommandsArguments();
            var result = await ExecuteCliRawAsync(executablePath, args, _defaultTimeout);

            if (result.Success)
            {
                ParseCommandList(result.Message);
                _commandsLoaded = true;
            }
            else
            {
                _logger.Log(LogLevel.Warn, $"Failed to load commands from {_cliCommand}: {result.Error}");
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, ex, $"Error loading commands from {_cliCommand}");
        }
    }

    private void ParseCommandList(string output)
    {
        try
        {
            var response = JsonSerializer.Deserialize(output, CommonJsonContext.Default.OperationResultListToolDefinition);
            if (response is null || !response.Success || response.Data is null) return;

            _tools.Clear();
            foreach (var toolDef in response.Data)
            {
                var metadata = new CliToolMetadata
                {
                    Name = toolDef.Name,
                    Description = toolDef.Description ?? string.Empty,
                    Category = toolDef.Category,
                    InputSchema = toolDef.InputSchema,
                    DefaultTimeout = (int)_defaultTimeout.TotalMilliseconds,
                    RequiredPermissions = [],
                    CliCommand = _cliCommand
                };
                _tools[metadata.Name] = metadata;
            }

            _logger.Log(LogLevel.Info, $"Loaded {_tools.Count} commands from {_cliCommand}");
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, ex, $"Failed to parse command list from {_cliCommand}");
        }
    }

    private static string BuildListToolsArguments() => CliDiscoveryRequestBuilder.BuildListToolsArguments();

    private static string BuildListCommandsArguments() => CliDiscoveryRequestBuilder.BuildListCommandsArguments();

    #endregion

    #region Configuration Fallback

    private void LoadToolsFromConfiguration(CliProviderConfiguration configuration)
    {
        foreach (var toolDef in configuration.Tools)
        {
            var metadata = new CliToolMetadata
            {
                Name = toolDef.Name,
                Description = toolDef.Description,
                Category = toolDef.Category,
                InputSchema = toolDef.InputSchema,
                DefaultTimeout = toolDef.DefaultTimeout ?? (int)configuration.GetTimeout().TotalMilliseconds,
                RequiredPermissions = toolDef.RequiredPermissions,
                CliCommand = configuration.CliCommand ?? configuration.Name,
                EnhancedDescription = toolDef.EnhancedDescription
            };

            _tools[metadata.Name] = metadata;
        }

        _commandsLoaded = _tools.Count > 0;
        _logger.Log(LogLevel.Info, $"Loaded {_tools.Count} tools from configuration for {_cliCommand}");
    }

    #endregion

    #region CLI Execution

    private string? ResolveExecutablePath()
    {
        if (!string.IsNullOrEmpty(_executablePath))
        {
            if (Path.IsPathRooted(_executablePath) && File.Exists(_executablePath))
            {
                return _executablePath;
            }

            var baseDir = AppContext.BaseDirectory;
            var combinedPath = Path.Combine(baseDir, _executablePath);
            if (File.Exists(combinedPath))
            {
                return combinedPath;
            }
        }

        return _packageManager.GetExecutablePath(_cliCommand);
    }

    private async Task<OperationResult> ExecuteCliAsync(
        string executablePath,
        IReadOnlyDictionary<string, JsonElement> parameters,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var args = BuildArguments(parameters);
        var stopwatch = Stopwatch.StartNew();

        var pool = _processPoolManager.GetOrCreatePool(_cliCommand, executablePath, _poolOptions);

        PooledProcess? pooledProcess = null;

        try
        {
            pooledProcess = await pool.AcquireAsync(timeout, cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(timeout);

            using var process = new Process { StartInfo = startInfo };

            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);

            await process.WaitForExitAsync(linkedCts.Token);
            stopwatch.Stop();

            var stdout = await stdoutTask ?? string.Empty;
            var stderr = await stderrTask ?? string.Empty;

            _logger.Log(LogLevel.Info, $"CLI executed in {stopwatch.ElapsedMilliseconds}ms, ExitCode: {process.ExitCode}, OutputLength: {stdout.Length}");

            var result = new OperationResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                Message = stdout,
                Error = string.IsNullOrEmpty(stderr) ? null : stderr
            };

            await ReleaseProcessAsync(pool, pooledProcess).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.Log(LogLevel.Error, $"CLI command timed out after {timeout.TotalSeconds}s");

            pooledProcess?.Kill();

            var result = OperationResultFactoryNonGeneric.Timeout(timeout, stopwatch.Elapsed.TotalMilliseconds);
            await ReleaseProcessAsync(pool, pooledProcess).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Log(LogLevel.Error, ex, "CLI execution failed");

            var result = OperationResultFactoryNonGeneric.FromException(ex, stopwatch.Elapsed.TotalMilliseconds);
            await ReleaseProcessAsync(pool, pooledProcess).ConfigureAwait(false);
            return result;
        }
    }

    private static async Task ReleaseProcessAsync(IProcessPool pool, PooledProcess? process)
    {
        if (process is not null)
        {
            await pool.ReleaseAsync(process).ConfigureAwait(false);
        }
    }

    private static async Task<OperationResult> ExecuteCliRawAsync(
        string executablePath,
        string arguments,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var cts = new CancellationTokenSource(timeout);
        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token);
            stopwatch.Stop();

            var stdout = await stdoutTask ?? string.Empty;
            var stderr = await stderrTask ?? string.Empty;

            return new OperationResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                Message = stdout,
                Error = string.IsNullOrEmpty(stderr) ? null : stderr
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();

            try { process.Kill(entireProcessTree: true); } catch { }

            return OperationResultFactoryNonGeneric.Timeout(timeout, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private static string BuildArguments(IReadOnlyDictionary<string, JsonElement> parameters) =>
        CliDiscoveryRequestBuilder.BuildCommandArguments(parameters);

    private static OperationResult CreateErrorResult(string error)
    {
        return OperationResultFactoryNonGeneric.CliFailure(error);
    }

    #endregion

    public void Dispose()
    {
        if (_disposed)
            return;

        _tools.Clear();
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _tools.Clear();
        _disposed = true;

        await Task.CompletedTask;
    }
}
