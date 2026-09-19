using Hlibz.EntityFrameworkCore.ModelRules.Internal;

using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Rules;

/// <summary>
/// MR004: every column stored as <see cref="string"/> - including enums and value objects
/// converted to one - has a max length (or an explicit column type such as <c>text</c>).
/// </summary>
internal sealed class StringsHaveMaxLengthRule() : ModelRule("MR004", "StringsHaveMaxLength")
{
    public override IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model)
    {
        foreach (ModelProperty property in ModelWalker.DeclaredProperties(model))
        {
            if (property.IsJson
                || ModelWalker.ProviderType(property.Property) != typeof(string)
                || property.Property.GetMaxLength() is not null
                || ModelWalker.HasExplicitColumnType(property.Property))
            {
                continue;
            }

            Type clrType = ModelWalker.ClrType(property.Property);
            string converted = clrType == typeof(string)
                ? string.Empty
                : $" (converted from {clrType.Name})";

            yield return Violation(
                property.EntityType,
                property.Path,
                $"string column{converted} has no max length. Configure HasMaxLength(...), or "
                + "HasColumnType(...) if an unbounded type is intended.");
        }
    }
}
