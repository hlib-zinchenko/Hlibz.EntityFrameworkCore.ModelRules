using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Rules;

/// <summary>
/// MR010: every entity type the predicate selects (typically those implementing a marker such as
/// <c>ISoftDeletable</c> or <c>ITenantOwned</c>) has a query filter. A new entity type that
/// forgets one leaks soft-deleted rows, or another tenant's data, into every query.
/// </summary>
internal sealed class EntitiesHaveQueryFilterRule(Func<Type, bool> requiresFilter, string reason)
    : ModelRule("MR010", "EntitiesHaveQueryFilter")
{
    public override IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model)
    {
        foreach (IReadOnlyEntityType entityType in model.GetEntityTypes())
        {
            // Owned types can't have a filter of their own: they're filtered through their owner.
            if (entityType.FindOwnership() is not null
                || !requiresFilter(entityType.ClrType))
            {
                continue;
            }

            // EF Core only allows query filters on the root of a hierarchy, where they apply to
            // every derived type.
            IReadOnlyEntityType root = entityType.GetRootType();
            if (HasQueryFilter(root))
            {
                continue;
            }

            yield return Violation(
                entityType,
                null,
                root == entityType
                    ? $"{reason} but has no query filter. Configure HasQueryFilter(...)."
                    : $"{reason} but its root type {root.DisplayName()} has no query filter, and "
                      + "EF Core only applies filters from the root. Configure HasQueryFilter(...) "
                      + $"on {root.DisplayName()}.");
        }
    }

    private static bool HasQueryFilter(IReadOnlyEntityType entityType)
    {
#if NET10_0_OR_GREATER
        return entityType.GetDeclaredQueryFilters().Count > 0;
#else
        return entityType.GetQueryFilter() is not null;
#endif
    }
}
