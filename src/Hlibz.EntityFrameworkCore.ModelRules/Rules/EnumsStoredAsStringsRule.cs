using Hlibz.EntityFrameworkCore.ModelRules.Internal;

using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Rules;

/// <summary>
/// MR006: every enum property is stored as a string, so reordering or inserting enum members
/// never silently changes the meaning of stored rows.
/// </summary>
internal sealed class EnumsStoredAsStringsRule() : ModelRule("MR006", "EnumsStoredAsStrings")
{
    public override IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model)
    {
        foreach (ModelProperty property in ModelWalker.DeclaredProperties(model))
        {
            Type clrType = ModelWalker.ClrType(property.Property);
            if (property.IsJson
                || !clrType.IsEnum
                || ModelWalker.ProviderType(property.Property) == typeof(string))
            {
                continue;
            }

            yield return Violation(
                property.EntityType,
                property.Path,
                $"enum {clrType.Name} is stored as its underlying number. Configure "
                + "HasConversion<string>(), or convert every enum at once with "
                + "configurationBuilder.Properties<Enum>().HaveConversion<string>().");
        }
    }
}
