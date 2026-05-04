namespace MemoryCli.Services;

internal sealed class MemoryIoService : IDisposable
{
    private static readonly System.Text.CompositeFormat s_lockTimeoutWriteFormat = System.Text.CompositeFormat.Parse(MessageTemplates.LockTimeoutWrite);
    private static readonly System.Text.CompositeFormat s_lockTimeoutSaveFormat = System.Text.CompositeFormat.Parse(MessageTemplates.LockTimeoutSave);
    private static readonly System.Text.CompositeFormat s_lockTimeoutFormat = System.Text.CompositeFormat.Parse(MessageTemplates.LockTimeout);

    private readonly MemoryOptions _options;
    private readonly string _memoryPattern;
    private readonly string _relationPattern;
    private readonly SemaphoreSlim _semaphore;
    private string _currentMemoryPath;
    private string _currentRelationPath;

    public MemoryIoService(MemoryOptions? options = null)
    {
        _options = options ?? new MemoryOptions();
        _memoryPattern = $"{_options.MemoryFileName.TrimEnd(FileExtensions.Jsonl.ToCharArray())}*{FileExtensions.Jsonl}";
        _relationPattern = $"{_options.RelationsFileName.TrimEnd(FileExtensions.Jsonl.ToCharArray())}*{FileExtensions.Jsonl}";

        FileOperationHelper.EnsureDirectory(_options.GetMemoryPath());
        _semaphore = new SemaphoreSlim(1, 1);
        _currentMemoryPath = _options.GetMemoryPath();
        _currentRelationPath = _options.GetRelationsPath();
    }

    public async Task<OperationResult<List<KnowledgeGraphEntity>>> LoadEntitiesAsync()
    {
        var lockAcquired = await TryAcquireLockAsync();
        if (!lockAcquired)
        {
            return CreateFallbackResult<List<KnowledgeGraphEntity>>([], []);
        }

        try
        {
            var (data, sourceFiles) = await LoadAllFromFilesAsync<KnowledgeGraphEntity>(_memoryPattern);
            return new OperationResult<List<KnowledgeGraphEntity>>
            {
                Success = true,
                Data = data,
                Message = string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    [nameof(sourceFiles)] = sourceFiles
                }
            };
        }
        finally
        {
            ReleaseLock();
        }
    }

    public async Task<OperationResult<List<KnowledgeGraphRelation>>> LoadRelationsAsync()
    {
        var lockAcquired = await TryAcquireLockAsync();
        if (!lockAcquired)
        {
            return CreateFallbackResult<List<KnowledgeGraphRelation>>([], []);
        }

        try
        {
            var (data, sourceFiles) = await LoadAllFromFilesAsync<KnowledgeGraphRelation>(_relationPattern);
            return new OperationResult<List<KnowledgeGraphRelation>>
            {
                Success = true,
                Data = data,
                Message = string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    [nameof(sourceFiles)] = sourceFiles
                }
            };
        }
        finally
        {
            ReleaseLock();
        }
    }

    public async Task<OperationResult<object>> AppendEntityAsync(KnowledgeGraphEntity entity)
    {
        var lockAcquired = await TryAcquireLockAsync();
        if (!lockAcquired)
        {
            SwitchToFallbackPaths();
        }

        try
        {
            await FileOperationHelper.AppendJsonLineAsync(
                _currentMemoryPath,
                entity,
                CommonJsonContext.Default.KnowledgeGraphEntity);

            return new OperationResult<object>
            {
                Success = true,
                Data = null!,
                Message = lockAcquired ? string.Empty : string.Format(null, s_lockTimeoutWriteFormat, MessageTemplates.BusyPrefix, _currentMemoryPath),
                Metadata = new Dictionary<string, object>
                {
                    ["isFallback"] = !lockAcquired,
                    ["sourceFiles"] = new List<string> { _currentMemoryPath }
                }
            };
        }
        finally
        {
            if (lockAcquired) ReleaseLock();
        }
    }

    public async Task<OperationResult<object>> AppendRelationAsync(KnowledgeGraphRelation relation)
    {
        var lockAcquired = await TryAcquireLockAsync();
        if (!lockAcquired)
        {
            SwitchToFallbackPaths();
        }

        try
        {
            await FileOperationHelper.AppendJsonLineAsync(
                _currentRelationPath,
                relation,
                CommonJsonContext.Default.KnowledgeGraphRelation);

            return new OperationResult<object>
            {
                Success = true,
                Data = null!,
                Message = lockAcquired ? string.Empty : string.Format(null, s_lockTimeoutWriteFormat, MessageTemplates.BusyPrefix, _currentRelationPath),
                Metadata = new Dictionary<string, object>
                {
                    ["isFallback"] = !lockAcquired,
                    ["sourceFiles"] = new List<string> { _currentRelationPath }
                }
            };
        }
        finally
        {
            if (lockAcquired) ReleaseLock();
        }
    }

    public async Task<OperationResult<object>> SaveEntitiesAsync(List<KnowledgeGraphEntity> entities)
    {
        var lockAcquired = await TryAcquireLockAsync();
        if (!lockAcquired)
        {
            SwitchToFallbackPaths();
        }

        try
        {
            await FileOperationHelper.SaveJsonLinesAsync(
                _currentMemoryPath,
                entities,
                CommonJsonContext.Default.KnowledgeGraphEntity);

            return new OperationResult<object>
            {
                Success = true,
                Data = null!,
                Message = lockAcquired ? string.Empty : string.Format(null, s_lockTimeoutSaveFormat, MessageTemplates.BusyPrefix, _currentMemoryPath),
                Metadata = new Dictionary<string, object>
                {
                    ["isFallback"] = !lockAcquired,
                    ["sourceFiles"] = new List<string> { _currentMemoryPath }
                }
            };
        }
        finally
        {
            if (lockAcquired) ReleaseLock();
        }
    }

    public async Task<OperationResult<object>> SaveRelationsAsync(List<KnowledgeGraphRelation> relations)
    {
        var lockAcquired = await TryAcquireLockAsync();
        if (!lockAcquired)
        {
            SwitchToFallbackPaths();
        }

        try
        {
            await FileOperationHelper.SaveJsonLinesAsync(
                _currentRelationPath,
                relations,
                CommonJsonContext.Default.KnowledgeGraphRelation);

            return new OperationResult<object>
            {
                Success = true,
                Data = null!,
                Message = lockAcquired ? string.Empty : string.Format(null, s_lockTimeoutSaveFormat, MessageTemplates.BusyPrefix, _currentRelationPath),
                Metadata = new Dictionary<string, object>
                {
                    ["isFallback"] = !lockAcquired,
                    ["sourceFiles"] = new List<string> { _currentRelationPath }
                }
            };
        }
        finally
        {
            if (lockAcquired) ReleaseLock();
        }
    }

    private void SwitchToFallbackPaths()
    {
        var timestamp = DateTime.Now.ToString(DateTimeFormats.FileTimestamp);
        _currentMemoryPath = Path.Combine(_options.BaseDirectory, $"{FileNames.Memory}_{timestamp}{FileExtensions.Jsonl}");
        _currentRelationPath = Path.Combine(_options.BaseDirectory, $"{FileNames.Memory}_{timestamp}_relations{FileExtensions.Jsonl}");
    }

    private OperationResult<T> CreateFallbackResult<T>(T data, List<string> sourceFiles)
    {
        return new OperationResult<T>
        {
            Success = true,
            Data = data,
            Message = string.Format(null, s_lockTimeoutFormat, MessageTemplates.BusyPrefix, _options.LockTimeout.TotalSeconds),
            Metadata = new Dictionary<string, object>
            {
                ["isFallback"] = true,
                ["sourceFiles"] = sourceFiles
            }
        };
    }

    private async Task<bool> TryAcquireLockAsync()
    {
        return await _semaphore.WaitAsync(_options.LockTimeout).ConfigureAwait(false);
    }

    private void ReleaseLock()
    {
        _semaphore.Release();
    }

    private async Task<(List<T> Data, List<string> SourceFiles)> LoadAllFromFilesAsync<T>(string searchPattern) where T : class
    {
        var allItems = new List<T>();
        var sourceFiles = new List<string>();
        var seenKeys = new HashSet<string>();

        var files = Directory.GetFiles(_options.BaseDirectory, searchPattern)
            .OrderBy(f => f)
            .ToList();

        foreach (var filePath in files)
        {
            if (!File.Exists(filePath))
                continue;

            var fileItems = await FileOperationHelper.ReadJsonLinesAsync<T>(
                filePath,
                GetTypeInfo<T>()).ConfigureAwait(false);

            var newItems = 0;

            foreach (var item in fileItems)
            {
                var key = GetItemKey(item);
                if (string.IsNullOrEmpty(key) || !seenKeys.Add(key))
                    continue;

                allItems.Add(item);
                newItems++;
            }

            if (newItems > 0)
                sourceFiles.Add(filePath);
        }

        return (allItems, sourceFiles);
    }

    private static string GetItemKey<T>(T item) where T : class
    {
        return item switch
        {
            KnowledgeGraphEntity e => e.Name.ToLowerInvariant(),
            KnowledgeGraphRelation r => $"{r.From.ToLowerInvariant()}{Separators.RelationKey}{r.To.ToLowerInvariant()}{Separators.RelationKey}{r.RelationType.ToLowerInvariant()}",
            _ => string.Empty
        };
    }

    private static System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> GetTypeInfo<T>() where T : class
    {
        if (typeof(T) == typeof(KnowledgeGraphEntity))
            return (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)(object)CommonJsonContext.Default.KnowledgeGraphEntity;
        if (typeof(T) == typeof(KnowledgeGraphRelation))
            return (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)(object)CommonJsonContext.Default.KnowledgeGraphRelation;
        throw new NotSupportedException($"Type {typeof(T)} not supported");
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
