using Hlibz.EntityFrameworkCore.ModelRules.Internal;

using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Rules;

/// <summary>
/// MR003: every column stored as <see cref="decimal"/> - including value objects converted to
/// one - has an explicit precision (or an explicit column type).
/// </summary>
internal sealed class DecimalsHavePrecisionRule() : ModelRule("MR003", "DecimalsHavePrecision")
{
    public override IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model)
    {
        foreach (ModelProperty property in ModelWalker.DeclaredProperties(model))
        {
            if (property.IsJson
                || ModelWalker.ProviderType(property.Property) != typeof(decimal)
                || property.Property.GetPrecision() is not null
                || ModelWalker.HasExplicitColumnType(property.Property))
            {
                continue;
            }

            yield return Violation(
                property.EntityType,
                property.Path,
                "decimal column has no precision, so its store type falls back to the provider's "
                + "default (unconstrained numeric on PostgreSQL, decimal(18,2) with silent "
                + "truncation on SQL Server). Configure HasPrecision(precision, scale).");
        }
    }
}
