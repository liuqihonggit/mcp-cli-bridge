using Common.Plugins;
using Common.Results;

namespace MemoryCli.Validation;

internal static partial class EntityValidator
{
    private static readonly Regex ValidNamePattern = GetValidNameRegex();

    public static ValidationResult ValidateEntity(KnowledgeGraphEntity entity)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(entity.Name))
        {
            errors.Add("Entity name cannot be empty");
        }
        else if (!IsValidName(entity.Name))
        {
            errors.Add($"Entity name '{entity.Name}' contains invalid characters. Only letters, numbers, spaces, hyphens and underscores are allowed");
        }
        else if (entity.Name.Length > 100)
        {
            errors.Add($"Entity name '{entity.Name}' is too long (max 100 characters)");
        }

        if (string.IsNullOrWhiteSpace(entity.EntityType))
        {
            errors.Add($"Entity type for '{entity.Name}' cannot be empty");
        }
        else if (!IsValidName(entity.EntityType))
        {
            errors.Add($"Entity type '{entity.EntityType}' contains invalid characters");
        }

        if (entity.Observations != null)
        {
            foreach (var observation in entity.Observations)
            {
                if (observation?.Length > 1000)
                {
                    errors.Add($"Observation for '{entity.Name}' is too long (max 1000 characters)");
                    break;
                }
            }
        }

        return errors.Count == 0
            ? ValidationResultFactory.Success()
            : ValidationResultFactory.Failure(errors);
    }

    public static ValidationResult ValidateRelation(KnowledgeGraphRelation relation, IReadOnlySet<string> existingEntityNames)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(relation.From))
        {
            errors.Add("Relation 'from' entity cannot be empty");
        }
        else if (!IsValidName(relation.From))
        {
            errors.Add($"Relation 'from' entity '{relation.From}' contains invalid characters");
        }

        if (string.IsNullOrWhiteSpace(relation.To))
        {
            errors.Add("Relation 'to' entity cannot be empty");
        }
        else if (!IsValidName(relation.To))
        {
            errors.Add($"Relation 'to' entity '{relation.To}' contains invalid characters");
        }

        if (string.IsNullOrWhiteSpace(relation.RelationType))
        {
            errors.Add("Relation type cannot be empty");
        }
        else if (!IsValidName(relation.RelationType))
        {
            errors.Add($"Relation type '{relation.RelationType}' contains invalid characters");
        }

        if (existingEntityNames.Count > 0)
        {
            if (!existingEntityNames.Contains(relation.From))
            {
                errors.Add($"Source entity '{relation.From}' does not exist");
            }
            if (!existingEntityNames.Contains(relation.To))
            {
                errors.Add($"Target entity '{relation.To}' does not exist");
            }
        }

        return errors.Count == 0
            ? ValidationResultFactory.Success()
            : ValidationResultFactory.Failure(errors);
    }

    public static bool IsValidName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && ValidNamePattern.IsMatch(name);
    }

    [GeneratedRegex(@"^[\w\s\-\u4e00-\u9fa5]+$", RegexOptions.Compiled)]
    private static partial Regex GetValidNameRegex();
}
