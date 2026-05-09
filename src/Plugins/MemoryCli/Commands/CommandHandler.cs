using Common.Contracts.Attributes;
using MemoryCli.Schemas;

namespace MemoryCli.Commands;

[CliCommandHandler("memory_cli", "Knowledge Graph CLI - Manage entities, relations, and observations in a persistent knowledge graph", Category = "knowledge-graph", ToolNamePrefix = "memory_", HasDocumentation = true)]
internal sealed partial class CommandHandler
{
    private static readonly System.Text.CompositeFormat s_partialBusyFormat = System.Text.CompositeFormat.Parse(MessageTemplates.PartialBusy);
    private static readonly System.Text.CompositeFormat s_deletedButBusyFormat = System.Text.CompositeFormat.Parse(MessageTemplates.DeletedButBusy);

    private readonly IKnowledgeGraphStore _store;
    private readonly MemoryOptions _options;

    public CommandHandler(IKnowledgeGraphStore store, MemoryOptions options)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    [CliCommand("create_entities", Description = "Create or update entities in the knowledge graph (upsert: same name overwrites)", Category = "knowledge-graph", SchemaType = typeof(MemorySchemas.CreateEntities))]
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

        var loadResult = await _store.LoadEntitiesAsync();
        if (loadResult.IsFallback)
            return Fail($"{MessageTemplates.BusyPrefix} {loadResult.Message}");

        var existingEntities = loadResult.Data ?? [];
        var existingNames = existingEntities
            .ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        var added = 0;
        var updated = 0;
        var needsSave = false;

        foreach (var entity in entities)
        {
            if (existingNames.TryGetValue(entity.Name, out var existing))
            {
                existingNames[entity.Name] = entity;
                var idx = existingEntities.IndexOf(existing);
                existingEntities[idx] = entity;
                updated++;
                needsSave = true;
            }
            else
            {
                var appendResult = await _store.AppendEntityAsync(entity);
                if (appendResult.IsFallback)
                    return Fail(string.Format(null, s_partialBusyFormat, MessageTemplates.BusyPrefix, added, "entities", appendResult.Message));

                existingNames[entity.Name] = entity;
                existingEntities.Add(entity);
                added++;
            }
        }

        if (needsSave)
        {
            var saveResult = await _store.SaveEntitiesAsync(existingEntities);
            if (saveResult.IsFallback)
                return Fail(string.Format(null, s_deletedButBusyFormat, MessageTemplates.BusyPrefix, "entities", saveResult.Message));
        }

        var message = updated > 0
            ? $"Created {added} entities, updated {updated} entities"
            : $"Created {added} entities";
        return Ok(new CountResult { Count = added, Updated = updated }, message, CommonJsonContext.Default.CountResult);
    }

    [CliCommand("create_relations", Description = "Create relations between entities", Category = "knowledge-graph", SchemaType = typeof(MemorySchemas.CreateRelations))]
    private async Task<OperationResult<JsonElement>> CreateRelationsAsync(CliRequest request)
    {
        var relations = request.Relations;
        if (relations == null || relations.Count == 0)
            return Fail("No relations provided");

        var entitiesResult = await _store.LoadEntitiesAsync();
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

        var loadResult = await _store.LoadRelationsAsync();
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

            var appendResult = await _store.AppendRelationAsync(relation);
            if (appendResult.IsFallback)
                return Fail(string.Format(null, s_partialBusyFormat, MessageTemplates.BusyPrefix, added, "relations", appendResult.Message));

            existingKeys.Add(key);
            added++;
        }

        return Ok(new CountResult { Count = added }, $"Created {added} relations", CommonJsonContext.Default.CountResult);
    }

    [CliCommand("read_graph", Description = "Read the entire knowledge graph", Category = "knowledge-graph", SchemaType = typeof(MemorySchemas.ReadGraph))]
    private async Task<OperationResult<JsonElement>> ReadGraphAsync()
    {
        var entitiesResult = await _store.LoadEntitiesAsync();
        var relationsResult = await _store.LoadRelationsAsync();

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

    [CliCommand("search_nodes", Description = "Search for nodes in the knowledge graph", Category = "knowledge-graph", SchemaType = typeof(MemorySchemas.SearchNodes))]
    private async Task<OperationResult<JsonElement>> SearchNodesAsync(CliRequest request)
    {
        var query = request.Query;
        if (string.IsNullOrWhiteSpace(query))
            return Fail("Query cannot be empty");

        var entitiesResult = await _store.LoadEntitiesAsync();
        var relationsResult = await _store.LoadRelationsAsync();

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

    [CliCommand("add_observations", Description = "Add observations to existing entities", Category = "knowledge-graph", SchemaType = typeof(MemorySchemas.AddObservations))]
    private async Task<OperationResult<JsonElement>> AddObservationsAsync(CliRequest request)
    {
        var name = request.Name;
        var observations = request.Observations;

        if (string.IsNullOrWhiteSpace(name) || observations == null || observations.Count == 0)
            return Fail("Invalid parameters");

        var loadResult = await _store.LoadEntitiesAsync();
        if (loadResult.IsFallback)
            return Fail($"{MessageTemplates.BusyPrefix} {loadResult.Message}");

        var entity = (loadResult.Data ?? []).FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (entity == null)
            return Fail($"Entity not found: {name}");

        entity.Observations.AddRange(observations);

        var saveResult = await _store.SaveEntitiesAsync(loadResult.Data ?? []);
        if (saveResult.IsFallback)
            return Fail(string.Format(null, s_deletedButBusyFormat, MessageTemplates.BusyPrefix, "observations", saveResult.Message));

        return Ok(new CountResult { Count = observations.Count }, $"Added {observations.Count} observations to {name}", CommonJsonContext.Default.CountResult);
    }

    [CliCommand("delete_entities", Description = "Delete entities from the graph", Category = "knowledge-graph", SchemaType = typeof(MemorySchemas.DeleteEntities))]
    private async Task<OperationResult<JsonElement>> DeleteEntitiesAsync(CliRequest request)
    {
        var names = request.Names;
        if (names == null || names.Count == 0)
            return Fail("No entities specified");

        var entitiesResult = await _store.LoadEntitiesAsync();
        var relationsResult = await _store.LoadRelationsAsync();

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

        var saveEntitiesResult = await _store.SaveEntitiesAsync(entities);
        if (saveEntitiesResult.IsFallback)
            return Fail(string.Format(null, s_deletedButBusyFormat, MessageTemplates.BusyPrefix, "entities", saveEntitiesResult.Message));

        var saveRelationsResult = await _store.SaveRelationsAsync(relations);
        if (saveRelationsResult.IsFallback)
            return Fail(string.Format(null, s_deletedButBusyFormat, MessageTemplates.BusyPrefix, "entities", saveRelationsResult.Message));

        return Ok(new DeleteResult { Deleted = originalCount - entities.Count }, $"Deleted {originalCount - entities.Count} entities and related relations", CommonJsonContext.Default.DeleteResult);
    }

    [CliCommand("delete_observations", Description = "Delete specific observations from entities", Category = "knowledge-graph", SchemaType = typeof(MemorySchemas.DeleteObservations))]
    private async Task<OperationResult<JsonElement>> DeleteObservationsAsync(CliRequest request)
    {
        var name = request.Name;
        var observations = request.Observations;

        if (string.IsNullOrWhiteSpace(name) || observations == null || observations.Count == 0)
            return Fail("Invalid parameters: name and observations are required");

        var loadResult = await _store.LoadEntitiesAsync();
        if (loadResult.IsFallback)
            return Fail($"{MessageTemplates.BusyPrefix} {loadResult.Message}");

        var entity = (loadResult.Data ?? []).FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (entity == null)
            return Fail($"Entity not found: {name}");

        var observationsToDelete = new HashSet<string>(observations, StringComparer.OrdinalIgnoreCase);
        var originalCount = entity.Observations?.Count ?? 0;

        entity.Observations?.RemoveAll(o => observationsToDelete.Contains(o));

        var deletedCount = originalCount - (entity.Observations?.Count ?? 0);

        var saveResult = await _store.SaveEntitiesAsync(loadResult.Data ?? []);
        if (saveResult.IsFallback)
            return Fail(string.Format(null, s_deletedButBusyFormat, MessageTemplates.BusyPrefix, "observations", saveResult.Message));

        return Ok(new DeleteResult { Deleted = deletedCount }, $"Deleted {deletedCount} observations from {name}", CommonJsonContext.Default.DeleteResult);
    }

    [CliCommand("delete_relations", Description = "Delete relations between entities", Category = "knowledge-graph", SchemaType = typeof(MemorySchemas.DeleteRelations))]
    private async Task<OperationResult<JsonElement>> DeleteRelationsAsync(CliRequest request)
    {
        var relations = request.Relations;
        if (relations == null || relations.Count == 0)
            return Fail("No relations provided");

        var loadResult = await _store.LoadRelationsAsync();
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

        var saveResult = await _store.SaveRelationsAsync(existingRelations);
        if (saveResult.IsFallback)
            return Fail(string.Format(null, s_deletedButBusyFormat, MessageTemplates.BusyPrefix, "relations", saveResult.Message));

        return Ok(new DeleteResult { Deleted = deletedCount }, $"Deleted {deletedCount} relations", CommonJsonContext.Default.DeleteResult);
    }

    [CliCommand("open_nodes", Description = "Get specific nodes by name", Category = "knowledge-graph", SchemaType = typeof(MemorySchemas.OpenNodes))]
    private async Task<OperationResult<JsonElement>> OpenNodesAsync(CliRequest request)
    {
        var names = request.Names;
        if (names == null || names.Count == 0)
            return Ok(new KnowledgeGraphData { Entities = [], Relations = [] }, "", CommonJsonContext.Default.KnowledgeGraphData);

        var entitiesResult = await _store.LoadEntitiesAsync();
        var relationsResult = await _store.LoadRelationsAsync();

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

    [CliCommand("get_storage_info", Description = "Get the storage location information for the knowledge graph", Category = "knowledge-graph", SchemaType = typeof(MemorySchemas.GetStorageInfo))]
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
}
