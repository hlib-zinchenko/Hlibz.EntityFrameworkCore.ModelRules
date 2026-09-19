using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Rules;

/// <summary>
/// MR008: deleting one aggregate root never cascades into another. EF Core defaults every required
/// relationship to cascade, so a lookup-style reference between roots (Country -> Currency) makes
/// deleting the lookup row wipe out every root that refers to it, and everything they own.
/// </summary>
internal sealed class NoCascadeDeleteAcrossAggregatesRule(Func<Type, bool> isAggregateRoot)
    : ModelRule("MR008", "NoCascadeDeleteAcrossAggregates")
{
    public override IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model)
    {
        foreach (IReadOnlyEntityType entityType in model.GetEntityTypes())
        {
            foreach (IReadOnlyForeignKey foreignKey in entityType.GetDeclaredForeignKeys())
            {
                if (foreignKey.IsOwnership
                    || foreignKey.DeleteBehavior is not (DeleteBehavior.Cascade
                        or DeleteBehavior.ClientCascade)
                    || !isAggregateRoot(foreignKey.PrincipalEntityType.ClrType)
                    || !isAggregateRoot(entityType.ClrType))
                {
                    continue;
                }

                yield return Violation(
                    entityType,
                    foreignKey.DependentToPrincipal?.Name
                    ?? string.Join(", ", foreignKey.Properties.Select(property => property.Name)),
                    $"deleting a {foreignKey.PrincipalEntityType.DisplayName()} cascades to "
                    + $"{entityType.DisplayName()}, a separate aggregate root. Configure "
                    + "OnDelete(DeleteBehavior.Restrict) (or SetNull for an optional "
                    + "relationship).");
            }
        }
    }
}
