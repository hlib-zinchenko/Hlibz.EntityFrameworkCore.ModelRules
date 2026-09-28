using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Rules;

/// <summary>
/// MR013: every aggregate root has a concurrency token. An aggregate is a consistency boundary;
/// without optimistic concurrency, two requests updating the same aggregate silently overwrite
/// each other's changes.
/// </summary>
internal sealed class AggregateRootsHaveConcurrencyTokenRule(Func<Type, bool> isAggregateRoot)
    : ModelRule("MR013", "AggregateRootsHaveConcurrencyToken")
{
    public override IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model)
    {
        foreach (IReadOnlyEntityType entityType in model.GetEntityTypes())
        {
            // One violation per hierarchy of roots, on its topmost root: a token declared there
            // covers every derived type. Every concrete type must end up with one.
            if (entityType.IsOwned()
                || !isAggregateRoot(entityType.ClrType)
                || (entityType.BaseType is { } baseType && isAggregateRoot(baseType.ClrType))
                || entityType.GetDerivedTypesInclusive()
                    .Where(type => !type.IsAbstract())
                    .All(type => type.GetProperties().Any(property => property.IsConcurrencyToken)))
            {
                continue;
            }

            yield return Violation(
                entityType,
                null,
                "aggregate root has no concurrency token, so concurrent updates silently "
                + "overwrite each other. Add a row version (IsRowVersion(), or xmin on "
                + "PostgreSQL), or mark a property IsConcurrencyToken().");
        }
    }
}
