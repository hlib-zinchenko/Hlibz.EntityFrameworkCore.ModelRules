using Hlibz.EntityFrameworkCore.ModelRules.Internal;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Rules;

/// <summary>
/// MR006: every enum property is stored as a string, so reordering or inserting enum members
/// never silently changes the meaning of stored rows. The same goes for the elements of a
/// primitive collection of enums (<c>List&lt;Status&gt;</c>, <c>Status[]</c>), which EF Core
/// stores as numbers in a JSON array or a database array. Enums inside a JSON document (an owned
/// or complex type mapped with <c>ToJson()</c>) are checked too: EF Core writes them as numbers
/// the same way, unless they are converted. An enum the provider stores as a database enum type
/// of its own, such as a PostgreSQL enum, passes, except inside a JSON-mapped owned type, where
/// it's written as a number.
/// </summary>
internal sealed class EnumsStoredAsStringsRule() : ModelRule("MR006", "EnumsStoredAsStrings")
{
    public override IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model)
    {
        // Unlike the column facet rules, this one includes properties stored inside JSON: a
        // value's type mapping decides how it's written there, just as for a column.
        foreach (ModelProperty property in ModelWalker.DeclaredProperties(model))
        {
            Type clrType = ModelWalker.ClrType(property.Property);
            if (clrType.IsEnum)
            {
                if (ModelWalker.ProviderType(property.Property) != typeof(string)
                    && !IsStoredAsNativeEnum(property.Property))
                {
                    yield return Violation(
                        property.EntityType,
                        property.Path,
                        $"enum {clrType.Name} is stored as its underlying number. Configure "
                        + "HasConversion<string>(), or convert every enum at once with "
                        + "configurationBuilder.Properties<Enum>().HaveConversion<string>().");
                }
            }
            else if (EnumElementStoredAsNumber(property.Property) is { } elementType)
            {
                yield return Violation(
                    property.EntityType,
                    property.Path,
                    $"collection of enum {elementType.Name} stores its elements as their "
                    + "underlying numbers. Configure "
                    + "PrimitiveCollection(...).ElementType().HasConversion<string>(); "
                    + "configurationBuilder.Properties<Enum>() doesn't reach collection elements.");
            }
        }
    }

    /// <summary>
    /// Whether a single enum property is stored by name because the provider maps it to a
    /// database enum type. Not inside a JSON-mapped owned type: there Npgsql writes such an enum
    /// as its number (EF Core 8 to 10), although it writes one inside a JSON complex type, and
    /// the elements of a collection anywhere, by name.
    /// </summary>
    private static bool IsStoredAsNativeEnum(IReadOnlyProperty property) =>
        ModelWalker.IsNativeEnum(property)
        && !(property.DeclaringType is IReadOnlyEntityType entityType
             && entityType.IsMappedToJson());

    /// <summary>
    /// The enum type of a primitive collection's elements, when they are stored as numbers:
    /// neither converted to strings nor mapped to a database enum type. <see langword="null"/>
    /// for anything else, including a collection converted to a single value of its own.
    /// </summary>
    private static Type? EnumElementStoredAsNumber(IReadOnlyProperty property)
    {
        if (property.GetElementType() is not { } element)
        {
            return null;
        }

        Type elementType = Nullable.GetUnderlyingType(element.ClrType) ?? element.ClrType;
        if (!elementType.IsEnum)
        {
            return null;
        }

        Type providerType = element.GetValueConverter()?.ProviderClrType
                            ?? element.GetProviderClrType()
                            ?? element.ClrType;
        if ((Nullable.GetUnderlyingType(providerType) ?? providerType) == typeof(string))
        {
            return null;
        }

        // A provider that stores the enum as a database enum type maps it with no converter,
        // the same way as for a single enum column.
        bool isNativeEnum = element.FindTypeMapping() is { Converter: null } mapping
                            && (Nullable.GetUnderlyingType(mapping.ClrType) ?? mapping.ClrType)
                            .IsEnum;

        return isNativeEnum ? null : elementType;
    }
}
