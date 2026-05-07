using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Common.CliProtocol;
using Common.Results;
using Common.Tools;
using Common.Contracts.Models;
using static MemoryCli.Schemas.MemoryToolSchemaTemplates;

namespace MemoryCli.Commands;

internal sealed class CommandHandler
{
    private static readonly System.Text.CompositeFormat s_partialBusyFormat = System.Text.CompositeFormat.Parse(MessageTemplates.PartialBusy);
    private static readonly System.Text.CompositeFormat s_deletedButBusyFormat = System.Text.CompositeFormat.Parse(MessageTemplates.DeletedButBusy);

    private readonly MemoryIoService _ioService;
    private readonly MemoryOptions _options;

    public CommandHandler(MemoryIoService ioService, MemoryOptions options)
    {
        _ioService = ioService ?? throw new ArgumentNullException(nameof(ioService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<OperationResult<JsonElement>> ExecuteAsync(CliRequest request)
    {
        return request.Command?.ToLowerInvariant() switch
        {
            "create_entities" => await CreateEntitiesAsync(request),
            "create_relations" => await CreateRelationsAsync(request),
            "read_graph" => await ReadGraphAsync(),
            "search_nodes" => await SearchNodesAsync(request),
            "add_observations" => await AddObservationsAsync(request),
            "delete_entities" => await DeleteEntitiesAsync(request),
            "delete_observations" => await DeleteObservationsAsync(request),
            "delete_relations" => await DeleteRelationsAsync(request),
            "open_nodes" => await OpenNodesAsync(request),
            "get_storage_info" => GetStorageInfo(),
            "list_tools" => ListTools(),
            "list_commands" => ListCommands(),
            _ => Fail($"Unknown command: {request.Command}")
        };
    }

    private static OperationResult<JsonElement> Fail(string message)
    {
        return new OperationResult<JsonElement>
        {
            Success = false,
            Message = message,
            Data = McpJsonSerializer.EmptyObject
        };
    }

    private static OperationResult<JsonElement> Ok<T>(T data, string message = "", JsonTypeInfo<T> typeInfo = null!)
    {
        return new OperationResult<JsonElement>
        {
            Success = true,
            Message = message,
            Data = JsonSerializer.SerializeToElement(data, typeInfo)
        };
    }

    private async Task<OperationResult<JsonElement>> CreateEntitiesAsync(CliRequest request)
    {
        var entities = request.Entities;
        if (entities == null || entities.Count == 0)
            return Fail("No entities provided");

        var validationErrors = entities
            .Select(EntityValidator.ValidateEntity)
            .Where(r => !r.IsValid)
            .SelectMany(r => r.Errors)
            .ToList();

        if (validationErrors.Count > 0)
            return Fail(string.Join("; ", validationErrors));

        var loadResult = await _ioService.LoadEntitiesAsync();
        if (loadResult.IsFallback)
            return Fail($"{MessageTemplates.BusyPrefix} {loadResult.Message}");

        var existingNames = (loadResult.Data ?? [])
            .ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var entity in entities)
        {
            if (existingNames.ContainsKey(entity.Name))
                continue;

            var appendResult = await _ioService.AppendEntityAsync(entity);
            if (appendResult.IsFallback)
                return Fail(string.Format(null, s_partialBusyFormat, MessageTemplates.BusyPrefix, added, "entities", appendResult.Message));

            existingNames[entity.Name] = entity;
            added++;
        }

        return Ok(new CountResult { Count = added }, $"Created {added} entities", CommonJsonContext.Default.CountResult);
    }

    private async Task<OperationResult<JsonElement>> CreateRelationsAsync(CliRequest request)
    {
        var relations = request.Relations;
        if (relations == null || relations.Count == 0)
            return Fail("No relations provided");

        var entitiesResult = await _ioService.LoadEntitiesAsync();
        if (entitiesResult.IsFallback)
            return Fail($"{MessageTemplates.BusyPrefix} {entitiesResult.Message}");

        var existingEntityNames = (entitiesResult.Data ?? []).Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var validationErrors = relations
            .Select(r => EntityValidator.ValidateRelation(r, existingEntityNames))
            .Where(r => !r.IsValid)
            .SelectMany(r => r.Errors)
            .ToList();

        if (validationErrors.Count > 0)
            return Fail(string.Join("; ", validationErrors));

        var loadResult = await _ioService.LoadRelationsAsync();
        if (loadResult.IsFallback)
            return Fail($"{MessageTemplates.BusyPrefix} {loadResult.Message}");

        var existingKeys = (loadResult.Data ?? [])
            .Select(r => $"{r.From}{Separators.RelationKey}{r.To}{Separators.RelationKey}{r.RelationType}")
            .ToHashSet(StringComparer.Ordinal);
        var added = 0;

        foreach (var relation in relations)
        {
            var key = $"{relation.From}{Separators.RelationKey}{relation.To}{Separators.RelationKey}{relation.RelationType}";
            if (existingKeys.Contains(key))
                continue;

            var appendResult = await _ioService.AppendRelationAsync(relation);
            if (appendResult.IsFallback)
                return Fail(string.Format(null, s_partialBusyFormat, MessageTemplates.BusyPrefix, added, "relations", appendResult.Message));

            existingKeys.Add(key);
            added++;
        }

        return Ok(new CountResult { Count = added }, $"Created {added} relations", CommonJsonContext.Default.CountResult);
    }

    private async Task<OperationResult<JsonElement>> ReadGraphAsync()
    {
        var entitiesResult = await _ioService.LoadEntitiesAsync();
        var relationsResult = await _ioService.LoadRelationsAsync();

        var message = string.Empty;
        if (entitiesResult.IsFallback || relationsResult.IsFallback)
            message = $"{MessageTemplates.BusyPrefix} {entitiesResult.Message} {relationsResult.Message}".Trim();

        var data = new KnowledgeGraphData
        {
            Entities = entitiesResult.Data ?? [],
            Relations = relationsResult.Data ?? [],
        };

        return Ok(data, message, CommonJsonContext.Default.KnowledgeGraphData);
    }

    private async Task<OperationResult<JsonElement>> SearchNodesAsync(CliRequest request)
    {
        var query = request.Query;
        if (string.IsNullOrWhiteSpace(query))
            return Fail("Query cannot be empty");

        var entitiesResult = await _ioService.LoadEntitiesAsync();
        var relationsResult = await _ioService.LoadRelationsAsync();

        var entities = entitiesResult.Data ?? [];
        var relations = relationsResult.Data ?? [];

        var matchedEntities = entities.Where(e =>
            e.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            e.EntityType.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            (e.Observations?.Any(o => o.Contains(query, StringComparison.OrdinalIgnoreCase)) ?? false)
        ).ToList();

        var matchedNames = matchedEntities.Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matchedRelations = relations.Where(r =>
            matchedNames.Contains(r.From) || matchedNames.Contains(r.To)
        ).ToList();

        var message = string.Empty;
        if (entitiesResult.IsFallback || relationsResult.IsFallback)
            message = $"{MessageTemplates.BusyPrefix} {entitiesResult.Message} {relationsResult.Message}".Trim();

        var data = new KnowledgeGraphData
        {
            Entities = matchedEntities,
            Relations = matchedRelations,
        };

        return Ok(data, message, CommonJsonContext.Default.KnowledgeGraphData);
    }

    private async Task<OperationResult<JsonElement>> AddObservationsAsync(CliRequest request)
    {
        var name = request.Name;
        var observations = request.Observations;

        if (string.IsNullOrWhiteSpace(name) || observations == null || observations.Count == 0)
            return Fail("Invalid parameters");

        var loadResult = await _ioService.LoadEntitiesAsync();
        if (loadResult.IsFallback)
            return Fail($"{MessageTemplates.BusyPrefix} {loadResult.Message}");

        var entity = (loadResult.Data ?? []).FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (entity == null)
            return Fail($"Entity not found: {name}");

        entity.Observations.AddRange(observations);

        var saveResult = await _ioService.SaveEntitiesAsync(loadResult.Data ?? []);
        if (saveResult.IsFallback)
            return Fail(string.Format(null, s_deletedButBusyFormat, MessageTemplates.BusyPrefix, "observations", saveResult.Message));

        return Ok(new CountResult { Count = observations.Count }, $"Added {observations.Count} observations to {name}", CommonJsonContext.Default.CountResult);
    }

    private async Task<OperationResult<JsonElement>> DeleteEntitiesAsync(CliRequest request)
    {
        var names = request.Names;
        if (names == null || names.Count == 0)
            return Fail("No entities specified");

        var entitiesResult = await _ioService.LoadEntitiesAsync();
        var relationsResult = await _ioService.LoadRelationsAsync();

        if (entitiesResult.IsFallback)
            return Fail($"{MessageTemplates.BusyPrefix} {entitiesResult.Message}");
        if (relationsResult.IsFallback)
            return Fail($"{MessageTemplates.BusyPrefix} {relationsResult.Message}");

        var entities = entitiesResult.Data ?? [];
        var relations = relationsResult.Data ?? [];

        var namesSet = new HashSet<string>(names.Select(n => n.ToLowerInvariant()));
        var originalCount = entities.Count;

        entities.RemoveAll(e => namesSet.Contains(e.Name.ToLowerInvariant()));
        relations.RemoveAll(r => namesSet.Contains(r.From.ToLowerInvariant()) || namesSet.Contains(r.To.ToLowerInvariant()));

        var saveEntitiesResult = await _ioService.SaveEntitiesAsync(entities);
        if (saveEntitiesResult.IsFallback)
            return Fail(string.Format(null, s_deletedButBusyFormat, MessageTemplates.BusyPrefix, "entities", saveEntitiesResult.Message));

        var saveRelationsResult = await _ioService.SaveRelationsAsync(relations);
        if (saveRelationsResult.IsFallback)
            return Fail(string.Format(null, s_deletedButBusyFormat, MessageTemplates.BusyPrefix, "entities", saveRelationsResult.Message));

        return Ok(new DeleteResult { Deleted = originalCount - entities.Count }, $"Deleted {originalCount - entities.Count} entities and related relations", CommonJsonContext.Default.DeleteResult);
    }

    private async Task<OperationResult<JsonElement>> DeleteObservationsAsync(CliRequest request)
    {
        var name = request.Name;
        var observations = request.Observations;

        if (string.IsNullOrWhiteSpace(name) || observations == null || observations.Count == 0)
            return Fail("Invalid parameters: name and observations are required");

        var loadResult = await _ioService.LoadEntitiesAsync();
        if (loadResult.IsFallback)
            return Fail($"{MessageTemplates.BusyPrefix} {loadResult.Message}");

        var entity = (loadResult.Data ?? []).FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (entity == null)
            return Fail($"Entity not found: {name}");

        var observationsToDelete = new HashSet<string>(observations, StringComparer.OrdinalIgnoreCase);
        var originalCount = entity.Observations?.Count ?? 0;

        entity.Observations?.RemoveAll(o => observationsToDelete.Contains(o));

        var deletedCount = originalCount - (entity.Observations?.Count ?? 0);

        var saveResult = await _ioService.SaveEntitiesAsync(loadResult.Data ?? []);
        if (saveResult.IsFallback)
            return Fail(string.Format(null, s_deletedButBusyFormat, MessageTemplates.BusyPrefix, "observations", saveResult.Message));

        return Ok(new DeleteResult { Deleted = deletedCount }, $"Deleted {deletedCount} observations from {name}", CommonJsonContext.Default.DeleteResult);
    }

    private async Task<OperationResult<JsonElement>> DeleteRelationsAsync(CliRequest request)
    {
        var relations = request.Relations;
        if (relations == null || relations.Count == 0)
            return Fail("No relations provided");

        var loadResult = await _ioService.LoadRelationsAsync();
        if (loadResult.IsFallback)
            return Fail($"{MessageTemplates.BusyPrefix} {loadResult.Message}");

        var existingRelations = loadResult.Data ?? [];
        var originalCount = existingRelations.Count;

        var relationsToDelete = relations
            .Select(r => $"{r.From}{Separators.RelationKey}{r.To}{Separators.RelationKey}{r.RelationType}")
            .ToHashSet(StringComparer.Ordinal);

        existingRelations.RemoveAll(r =>
        {
            var key = $"{r.From}{Separators.RelationKey}{r.To}{Separators.RelationKey}{r.RelationType}";
            return relationsToDelete.Contains(key);
        });

        var deletedCount = originalCount - existingRelations.Count;

        var saveResult = await _ioService.SaveRelationsAsync(existingRelations);
        if (saveResult.IsFallback)
            return Fail(string.Format(null, s_deletedButBusyFormat, MessageTemplates.BusyPrefix, "relations", saveResult.Message));

        return Ok(new DeleteResult { Deleted = deletedCount }, $"Deleted {deletedCount} relations", CommonJsonContext.Default.DeleteResult);
    }

    private async Task<OperationResult<JsonElement>> OpenNodesAsync(CliRequest request)
    {
        var names = request.Names;
        if (names == null || names.Count == 0)
            return Ok(new KnowledgeGraphData { Entities = [], Relations = [] }, "", CommonJsonContext.Default.KnowledgeGraphData);

        var entitiesResult = await _ioService.LoadEntitiesAsync();
        var relationsResult = await _ioService.LoadRelationsAsync();

        var entities = entitiesResult.Data ?? [];
        var relations = relationsResult.Data ?? [];

        var namesSet = new HashSet<string>(names.Select(n => n.ToLowerInvariant()));
        var matchedEntities = entities.Where(e => namesSet.Contains(e.Name.ToLowerInvariant())).ToList();
        var matchedNames = matchedEntities.Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matchedRelations = relations.Where(r =>
            matchedNames.Contains(r.From) || matchedNames.Contains(r.To)
        ).ToList();

        var message = string.Empty;
        if (entitiesResult.IsFallback || relationsResult.IsFallback)
            message = $"{MessageTemplates.BusyPrefix} {entitiesResult.Message} {relationsResult.Message}".Trim();

        var data = new KnowledgeGraphData
        {
            Entities = matchedEntities,
            Relations = matchedRelations,
        };

        return Ok(data, message, CommonJsonContext.Default.KnowledgeGraphData);
    }

    private OperationResult<JsonElement> GetStorageInfo()
    {
        var info = new StorageInfo
        {
            BaseDirectory = _options.BaseDirectory,
            MemoryFilePath = _options.GetMemoryPath(),
            RelationsFilePath = _options.GetRelationsPath(),
            EnvironmentVariable = "MCP_MEMORY_PATH"
        };

        return Ok(info, "", CommonJsonContext.Default.StorageInfo);
    }

    private static OperationResult<JsonElement> ListTools()
    {
        var pluginDescriptor = new PluginDescriptor
        {
            Name = "memory",
            Description = "Knowledge Graph CLI - Manage entities, relations, and observations in a persistent knowledge graph",
            Category = "knowledge-graph",
            CommandCount = 10,
            HasDocumentation = true
        };

        return Ok(pluginDescriptor, "", CommonJsonContext.Default.PluginDescriptor);
    }

    private static OperationResult<JsonElement> ListCommands()
    {
        var tools = new List<ToolDefinition>
        {
            new()
            {
                Name = "memory_create_entities",
                Description = "Create multiple new entities in the knowledge graph",
                Category = "knowledge-graph",
                InputSchema = CreateEntitiesSchema()
            },
            new()
            {
                Name = "memory_create_relations",
                Description = "Create relations between entities",
                Category = "knowledge-graph",
                InputSchema = CreateRelationsSchema()
            },
            new()
            {
                Name = "memory_read_graph",
                Description = "Read the entire knowledge graph",
                Category = "knowledge-graph",
                InputSchema = ReadGraphSchema()
            },
            new()
            {
                Name = "memory_search_nodes",
                Description = "Search for nodes in the knowledge graph",
                Category = "knowledge-graph",
                InputSchema = SearchNodesSchema()
            },
            new()
            {
                Name = "memory_add_observations",
                Description = "Add observations to existing entities",
                Category = "knowledge-graph",
                InputSchema = AddObservationsSchema()
            },
            new()
            {
                Name = "memory_delete_entities",
                Description = "Delete entities from the graph",
                Category = "knowledge-graph",
                InputSchema = DeleteEntitiesSchema()
            },
            new()
            {
                Name = "memory_delete_observations",
                Description = "Delete specific observations from entities",
                Category = "knowledge-graph",
                InputSchema = DeleteObservationsSchema()
            },
            new()
            {
                Name = "memory_delete_relations",
                Description = "Delete relations between entities",
                Category = "knowledge-graph",
                InputSchema = DeleteRelationsSchema()
            },
            new()
            {
                Name = "memory_open_nodes",
                Description = "Get specific nodes by name",
                Category = "knowledge-graph",
                InputSchema = OpenNodesSchema()
            },
            new()
            {
                Name = "memory_get_storage_info",
                Description = "Get the storage location information for the knowledge graph",
                Category = "knowledge-graph",
                InputSchema = GetStorageInfoSchema()
            }
        };

        return Ok(tools, "", CommonJsonContext.Default.ListToolDefinition);
    }
}
