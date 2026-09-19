using Hlibz.EntityFrameworkCore.ModelRules.Internal;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Rules;

/// <summary>
/// MR007: every table and view of the model lives in one schema - the expected one when given,
/// otherwise whichever schema most tables use. Useful in a modular monolith where each module's
/// DbContext owns exactly one schema.
/// </summary>
internal sealed class SingleSchemaRule(string? expectedSchema) : ModelRule("MR007", "SingleSchema")
{
    private const string DefaultSchemaName = "(default schema)";

    public override IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model)
    {
        string? defaultSchema = model.GetDefaultSchema();

        List<(IReadOnlyEntityType EntityType, string? Schema)> mappings =
        [
            .. ModelIdentifiers.Collect(model)
                .Where(identifier => identifier.Scope == NamingScope.Tables)
                .Select(identifier => (identifier.EntityType, identifier.Schema ?? defaultSchema)),
        ];

        if (mappings.Count == 0)
        {
            yield break;
        }

        string? expected = expectedSchema
                           ?? mappings
                               .GroupBy(mapping => mapping.Schema)
                               .OrderByDescending(group => group.Count())
                               .First()
                               .Key;

        foreach ((IReadOnlyEntityType entityType, string? schema) in mappings)
        {
            if (!string.Equals(schema, expected, StringComparison.Ordinal))
            {
                yield return Violation(
                    entityType,
                    null,
                    $"mapped to schema '{schema ?? DefaultSchemaName}', but the model's tables "
                    + $"belong in '{expected ?? DefaultSchemaName}'.");
            }
        }
    }
}
