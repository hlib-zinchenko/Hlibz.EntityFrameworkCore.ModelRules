using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Rules;

/// <summary>
/// MR001: every mapped property has a CLR member behind it. Shadow properties are usually foreign
/// keys EF invented because a relationship's key property was missing or misnamed.
/// </summary>
internal sealed class NoShadowPropertiesRule() : ModelRule("MR001", "NoShadowProperties")
{
    private const string SqlServerIsTemporal = "SqlServer:IsTemporal";
    private const string SqlServerPeriodStart = "SqlServer:TemporalPeriodStartPropertyName";
    private const string SqlServerPeriodEnd = "SqlServer:TemporalPeriodEndPropertyName";

    public override IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model)
    {
        foreach (IReadOnlyEntityType entityType in model.GetEntityTypes())
        {
            foreach (IReadOnlyProperty property in entityType.GetDeclaredProperties())
            {
                if (!property.IsShadowProperty()
                    || property.IsIndexerProperty()
                    || IsUnavoidable(entityType, property))
                {
                    continue;
                }

                string message = property.GetContainingForeignKeys()
                    .FirstOrDefault() is { } foreignKey
                    ? $"shadow foreign key created by convention for the relationship to "
                      + $"{foreignKey.PrincipalEntityType.DisplayName()}. Add a '{property.Name}' "
                      + "property to the entity, or configure the relationship with "
                      + "HasForeignKey(...) pointing at an existing one."
                    : $"shadow property '{property.Name}' has no CLR property or field behind it. "
                      + "Map a real member instead.";

                yield return Violation(entityType, property.Name, message);
            }
        }
    }

    /// <summary>
    /// Shadow properties EF needs and the user cannot replace with a CLR member without fighting
    /// the framework: TPH discriminators, owned types' synthetic keys, and SQL Server temporal
    /// tables' period columns.
    /// </summary>
    private static bool IsUnavoidable(IReadOnlyEntityType entityType, IReadOnlyProperty property)
    {
        if (entityType.FindDiscriminatorProperty() == property
            || IsSqlServerTemporalPeriod(entityType, property))
        {
            return true;
        }

        return entityType.FindOwnership() is { } ownership
               && (property.IsPrimaryKey() || ownership.Properties.Contains(property));
    }

    /// <summary>
    /// Whether the property is the period start/end column of a SQL Server temporal table. Read
    /// through the provider's annotation names, so this package needn't reference the SQL Server
    /// provider.
    /// </summary>
    private static bool IsSqlServerTemporalPeriod(
        IReadOnlyEntityType entityType,
        IReadOnlyProperty property)
    {
        IReadOnlyEntityType root = entityType.GetRootType();
        if (root.FindAnnotation(SqlServerIsTemporal)?.Value is not true)
        {
            return false;
        }

        return string.Equals(
                   root.FindAnnotation(SqlServerPeriodStart)?.Value as string,
                   property.Name,
                   StringComparison.Ordinal)
               || string.Equals(
                   root.FindAnnotation(SqlServerPeriodEnd)?.Value as string,
                   property.Name,
                   StringComparison.Ordinal);
    }
}
