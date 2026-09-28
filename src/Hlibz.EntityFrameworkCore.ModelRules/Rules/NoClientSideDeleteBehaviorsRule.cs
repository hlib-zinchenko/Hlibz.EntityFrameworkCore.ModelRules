using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Rules;

/// <summary>
/// MR011: no relationship uses a delete behavior that only EF Core's change tracker carries out.
/// <see cref="DeleteBehavior.ClientSetNull"/> (EF Core's default for optional relationships) and
/// <see cref="DeleteBehavior.ClientCascade"/> leave the database constraint at NO ACTION, so they
/// only affect dependents that happen to be loaded; deleting a principal whose dependents aren't
/// tracked, or deleting from SQL or <c>ExecuteDelete</c>, fails with a foreign key violation.
/// </summary>
internal sealed class NoClientSideDeleteBehaviorsRule()
    : ModelRule("MR011", "NoClientSideDeleteBehaviors")
{
    public override IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model)
    {
        foreach (IReadOnlyEntityType entityType in model.GetEntityTypes())
        {
            foreach (IReadOnlyForeignKey foreignKey in entityType.GetDeclaredForeignKeys())
            {
                if (foreignKey.IsOwnership
                    || foreignKey.DeleteBehavior is not (DeleteBehavior.ClientSetNull
                        or DeleteBehavior.ClientCascade)
                    || !IsDatabaseConstraint(foreignKey))
                {
                    continue;
                }

                string principal = foreignKey.PrincipalEntityType.DisplayName();
                string effect = foreignKey.DeleteBehavior == DeleteBehavior.ClientSetNull
                    ? $"sets the foreign key to null only on {entityType.DisplayName()} rows EF "
                      + "Core is tracking"
                    : $"cascades only to {entityType.DisplayName()} rows EF Core is tracking";
                string fix = foreignKey.DeleteBehavior == DeleteBehavior.ClientSetNull
                    ? "OnDelete(DeleteBehavior.SetNull)"
                    : "OnDelete(DeleteBehavior.Cascade)";

                yield return Violation(
                    entityType,
                    foreignKey.DependentToPrincipal?.Name
                    ?? string.Join(", ", foreignKey.Properties.Select(property => property.Name)),
                    $"deleting {principal} rows {effect}; the database constraint does nothing, so "
                    + "the delete fails when any other row still refers to it. Configure "
                    + $"{fix}, or OnDelete(DeleteBehavior.Restrict) to forbid it.");
            }
        }
    }

    /// <summary>
    /// Whether both sides are mapped to tables, so the database actually has the constraint. The
    /// dependent side counts derived types: under TPC, a foreign key declared on an abstract base
    /// type is created on every concrete table.
    /// </summary>
    private static bool IsDatabaseConstraint(IReadOnlyForeignKey foreignKey) =>
        foreignKey.DeclaringEntityType.GetDerivedTypesInclusive()
            .Any(entityType => StoreObjectIdentifier.Create(entityType, StoreObjectType.Table)
                is not null)
        && StoreObjectIdentifier.Create(foreignKey.PrincipalEntityType, StoreObjectType.Table)
            is not null;
}
