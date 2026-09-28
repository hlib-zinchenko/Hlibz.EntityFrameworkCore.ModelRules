using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Rules;

/// <summary>
/// MR012: no navigation leads from one aggregate root to another. Aggregates reference each other
/// by key only; a navigation invites loading, and changing, two aggregates in one unit of work.
/// Navigations inside an aggregate (a root to its own children and back) are fine.
/// </summary>
internal sealed class NoNavigationsAcrossAggregatesRule(Func<Type, bool> isAggregateRoot)
    : ModelRule("MR012", "NoNavigationsAcrossAggregates")
{
    public override IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model)
    {
        foreach (IReadOnlyEntityType entityType in model.GetEntityTypes())
        {
            if (!isAggregateRoot(entityType.ClrType))
            {
                continue;
            }

            IEnumerable<IReadOnlyNavigationBase> navigations =
            [
                .. entityType.GetDeclaredNavigations(),
                .. entityType.GetDeclaredSkipNavigations(),
            ];

            foreach (IReadOnlyNavigationBase navigation in navigations)
            {
                IReadOnlyEntityType target = navigation.TargetEntityType;
                if (target.IsOwned() || !isAggregateRoot(target.ClrType))
                {
                    continue;
                }

                yield return Violation(
                    entityType,
                    navigation.Name,
                    $"navigation to {target.DisplayName()}, a separate aggregate root. Reference "
                    + "it by key only: keep the foreign key property, remove the navigation, and "
                    + $"load {target.DisplayName()} through its own repository.");
            }
        }
    }
}
