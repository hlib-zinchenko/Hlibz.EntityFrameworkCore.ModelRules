using Hlibz.EntityFrameworkCore.ModelRules.Internal;

using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Rules;

/// <summary>
/// MR009: no database identifier in scope is longer than the database allows (63 on PostgreSQL,
/// 128 on SQL Server). EF Core shortens the names it generates, but not names configured
/// explicitly, which PostgreSQL then truncates silently.
/// </summary>
internal sealed class MaxIdentifierLengthRule(int maxLength, NamingScope scope)
    : ModelRule("MR009", "MaxIdentifierLength")
{
    public override IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model)
    {
        foreach (ModelIdentifier identifier in ModelIdentifiers.Collect(model))
        {
            if ((scope & identifier.Scope) != 0 && identifier.Name.Length > maxLength)
            {
                yield return Violation(
                    identifier.EntityType,
                    identifier.MemberPath,
                    $"{identifier.Kind} name '{identifier.Name}' is {identifier.Name.Length} "
                    + $"characters long; the limit is {maxLength}.");
            }
        }
    }
}
