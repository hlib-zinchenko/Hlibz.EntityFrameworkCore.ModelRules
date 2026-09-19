using System.Reflection;

using Hlibz.EntityFrameworkCore.ModelRules.Internal;

using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Rules;

/// <summary>
/// MR005: a property's C# nullability (nullable reference type annotations, or
/// <see cref="Nullable{T}"/>) agrees with whether its column allows NULL. Members in code without
/// nullable annotations are skipped.
/// </summary>
internal sealed class NullabilityMatchesClrRule() : ModelRule("MR005", "NullabilityMatchesClr")
{
    public override IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model)
    {
        // Not thread-safe, so one per run rather than one per rule instance.
        NullabilityInfoContext nullabilityContext = new();

        foreach (ModelProperty property in ModelWalker.DeclaredProperties(model))
        {
            if (IsClrNullable(property.Property, nullabilityContext) is not { } clrNullable
                || clrNullable == property.Property.IsNullable)
            {
                continue;
            }

            yield return Violation(
                property.EntityType,
                property.Path,
                clrNullable
                    ? "C# type is nullable but the column is NOT NULL. Make the C# type "
                      + "non-nullable, or remove IsRequired()."
                    : "C# type is non-nullable but the column allows NULL. Make the C# type "
                      + "nullable, or configure IsRequired().");
        }
    }

    private static bool? IsClrNullable(IReadOnlyProperty property, NullabilityInfoContext context)
    {
        if (property.ClrType.IsValueType)
        {
            return property.PropertyInfo is null && property.FieldInfo is null
                ? null
                : Nullable.GetUnderlyingType(property.ClrType) is not null;
        }

        NullabilityInfo? info = property.PropertyInfo is { } propertyInfo
            ? context.Create(propertyInfo)
            : property.FieldInfo is { } fieldInfo
                ? context.Create(fieldInfo)
                : null;

        return info?.ReadState switch
        {
            NullabilityState.Nullable => true,
            NullabilityState.NotNull => false,
            _ => null,
        };
    }
}
