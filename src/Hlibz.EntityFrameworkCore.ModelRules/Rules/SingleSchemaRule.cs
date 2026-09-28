using Hlibz.EntityFrameworkCore.ModelRules.Internal;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Rules;

/// <summary>
/// MR007: every table, view, sequence and database function of the model lives in one schema -
/// the expected one when given, otherwise whichever schema most tables use. Useful in a modular
/// monolith where each module's DbContext owns exactly one schema.
/// </summary>
internal sealed class SingleSchemaRule(string? expectedSchema) : ModelRule("MR007", "SingleSchema")
{
    private const string DefaultSchemaName = "(default schema)";

    private const NamingScope SchemaBoundObjects =
        NamingScope.Tables | NamingScope.Sequences | NamingScope.Functions;

    public override IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model)
    {
        string? defaultSchema = model.GetDefaultSchema();

        List<ModelIdentifier> objects =
        [
            .. ModelIdentifiers.Collect(model)
                .Where(identifier => (identifier.Scope & SchemaBoundObjects) != 0),
        ];

        if (objects.Count == 0)
        {
            yield break;
        }

        // The majority is decided by tables; sequences and functions only count when the model
        // has no tables at all.
        List<ModelIdentifier> tables =
            [.. objects.Where(identifier => identifier.Scope == NamingScope.Tables)];

        string? expected = expectedSchema
                           ?? (tables.Count > 0 ? tables : objects)
                               .GroupBy(identifier => identifier.Schema ?? defaultSchema)
                               .OrderByDescending(group => group.Count())
                               .First()
                               .Key;

        foreach (ModelIdentifier identifier in objects)
        {
            string? schema = identifier.Schema ?? defaultSchema;
            if (!string.Equals(schema, expected, StringComparison.Ordinal))
            {
                yield return Violation(
                    identifier,
                    $"mapped to schema '{schema ?? DefaultSchemaName}', but the model's tables "
                    + $"belong in '{expected ?? DefaultSchemaName}'.");
            }
        }
    }
}
