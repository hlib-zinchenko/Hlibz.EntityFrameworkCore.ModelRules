using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Internal;

/// <summary>
/// A scalar property together with the entity type a violation on it is reported against and its
/// dotted path from that entity type.
/// </summary>
/// <param name="EntityType">
/// The entity type that declares the property, or that contains the complex property it belongs
/// to.
/// </param>
/// <param name="Property">The scalar property.</param>
/// <param name="Path">The dotted member path from <paramref name="EntityType"/>.</param>
/// <param name="IsJson">
/// Whether the property is stored inside a JSON document rather than in a column of its own.
/// </param>
internal sealed record ModelProperty(
    IReadOnlyEntityType EntityType,
    IReadOnlyProperty Property,
    string Path,
    bool IsJson);

/// <summary>
/// Walks model metadata in the shapes the built-in rules need.
/// </summary>
internal static class ModelWalker
{
    /// <summary>
    /// Every scalar property declared anywhere in the model, including properties of complex types
    /// (recursively), each exactly once.
    /// </summary>
    public static IEnumerable<ModelProperty> DeclaredProperties(IReadOnlyModel model)
    {
        foreach (IReadOnlyEntityType entityType in model.GetEntityTypes())
        {
            bool isJson = entityType.IsMappedToJson();

            foreach (IReadOnlyProperty property in entityType.GetDeclaredProperties())
            {
                yield return new ModelProperty(entityType, property, property.Name, isJson);
            }

            foreach (ModelProperty property in ComplexProperties(
                         entityType,
                         entityType.GetDeclaredComplexProperties(),
                         string.Empty,
                         isJson,
                         declaredOnly: true))
            {
                yield return property;
            }
        }
    }

    /// <summary>
    /// Every scalar property of one entity type, inherited ones included, each reported against the
    /// entity type that declares it.
    /// </summary>
    public static IEnumerable<ModelProperty> AllProperties(IReadOnlyEntityType entityType)
    {
        bool isJson = entityType.IsMappedToJson();

        foreach (IReadOnlyProperty property in entityType.GetProperties())
        {
            yield return new ModelProperty(
                (IReadOnlyEntityType)property.DeclaringType,
                property,
                property.Name,
                isJson);
        }

        foreach (IReadOnlyComplexProperty complexProperty in entityType.GetComplexProperties())
        {
            foreach (ModelProperty property in ComplexProperties(
                         (IReadOnlyEntityType)complexProperty.DeclaringType,
                         [complexProperty],
                         string.Empty,
                         isJson,
                         declaredOnly: false))
            {
                yield return property;
            }
        }
    }

    /// <summary>
    /// The underlying non-nullable type of the value as it is stored: the value converter's (or
    /// configured) provider type when there is one, otherwise the CLR type.
    /// </summary>
    public static Type ProviderType(IReadOnlyProperty property)
    {
        Type type = property.GetValueConverter()?.ProviderClrType
                    ?? property.GetProviderClrType()
                    ?? property.ClrType;

        return Nullable.GetUnderlyingType(type) ?? type;
    }

    /// <summary>
    /// The underlying non-nullable CLR type of the property.
    /// </summary>
    public static Type ClrType(IReadOnlyProperty property) =>
        Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

    /// <summary>
    /// Whether the property has an explicitly configured store type, e.g. via
    /// <c>HasColumnType("text")</c>. Rules about store facets treat that as a deliberate choice.
    /// A foreign key property inherits it from the principal key, as EF Core's type mapping does.
    /// </summary>
    public static bool HasExplicitColumnType(IReadOnlyProperty property) =>
        property.GetPrincipals().Any(principal =>
            principal.FindAnnotation(RelationalAnnotationNames.ColumnType)?.Value is string);

    /// <summary>
    /// Whether the column has a max length: configured on the property, inherited from the
    /// principal key of a foreign key property, or implied by its value converter (a
    /// <see cref="Guid"/> converted to a string is 36 characters).
    /// </summary>
    public static bool HasMaxLength(IReadOnlyProperty property) =>
        property.GetPrincipals().Any(principal => principal.GetMaxLength() is not null)
        || property.FindTypeMapping()?.Converter?.MappingHints?.Size is not null;

    /// <summary>
    /// Whether the column has a precision: configured on the property, inherited from the
    /// principal key of a foreign key property, or implied by its value converter.
    /// </summary>
    public static bool HasPrecision(IReadOnlyProperty property) =>
        property.GetPrincipals().Any(principal => principal.GetPrecision() is not null)
        || property.FindTypeMapping()?.Converter?.MappingHints?.Precision is not null;

    /// <summary>
    /// Whether the provider stores the enum property as a database enum type of its own (e.g.
    /// PostgreSQL enums mapped with Npgsql's <c>MapEnum</c>): its type mapping takes the enum
    /// as is, with no value converter. A provider that stores an enum as a number maps it with
    /// a converter to the underlying type.
    /// </summary>
    public static bool IsNativeEnum(IReadOnlyProperty property) =>
        property.FindTypeMapping() is { Converter: null } mapping
        && (Nullable.GetUnderlyingType(mapping.ClrType) ?? mapping.ClrType).IsEnum;

    /// <summary>
    /// Whether the foreign key is a link EF Core creates between the tables of one entity: from
    /// a TPT-derived type's table to its base type's, or from a table an entity type is split
    /// into (<c>SplitToTable</c>) to its main table. It belongs to the mapping, not to a
    /// relationship: it can't be configured, and both ends are the same row of the same entity.
    /// </summary>
    public static bool IsMappingLink(IReadOnlyForeignKey foreignKey) =>
        foreignKey.PrincipalEntityType.IsAssignableFrom(foreignKey.DeclaringEntityType)
        && foreignKey.PrincipalKey.IsPrimaryKey()
        && foreignKey.DeclaringEntityType.FindPrimaryKey() is { } primaryKey
        && primaryKey.Properties.SequenceEqual(foreignKey.Properties);

    /// <summary>
    /// The tables (or views) the entity type is mapped to: its main one, plus any it is split
    /// into with <c>SplitToTable</c> or <c>SplitToView</c>.
    /// </summary>
    public static IEnumerable<StoreObjectIdentifier> StoreObjects(
        IReadOnlyEntityType entityType,
        StoreObjectType storeObjectType)
    {
        if (StoreObjectIdentifier.Create(entityType, storeObjectType) is not { } main)
        {
            yield break;
        }

        yield return main;

        foreach (IReadOnlyEntityTypeMappingFragment fragment in
                 entityType.GetMappingFragments(storeObjectType))
        {
            yield return fragment.StoreObject;
        }
    }

    private static IEnumerable<ModelProperty> ComplexProperties(
        IReadOnlyEntityType entityType,
        IEnumerable<IReadOnlyComplexProperty> complexProperties,
        string prefix,
        bool isJson,
        bool declaredOnly)
    {
        foreach (IReadOnlyComplexProperty complexProperty in complexProperties)
        {
            string path = prefix + complexProperty.Name;
            IReadOnlyComplexType complexType = complexProperty.ComplexType;
            bool complexIsJson = isJson || IsJson(complexProperty);

            IEnumerable<IReadOnlyProperty> properties = declaredOnly
                ? complexType.GetDeclaredProperties()
                : complexType.GetProperties();
            foreach (IReadOnlyProperty property in properties)
            {
                yield return new ModelProperty(
                    entityType,
                    property,
                    $"{path}.{property.Name}",
                    complexIsJson);
            }

            IEnumerable<IReadOnlyComplexProperty> nested = declaredOnly
                ? complexType.GetDeclaredComplexProperties()
                : complexType.GetComplexProperties();
            foreach (ModelProperty property in ComplexProperties(
                         entityType,
                         nested,
                         path + ".",
                         complexIsJson,
                         declaredOnly))
            {
                yield return property;
            }
        }
    }

    private static bool IsJson(IReadOnlyComplexProperty complexProperty)
    {
#if NET10_0_OR_GREATER
        return complexProperty.IsCollection || complexProperty.ComplexType.IsMappedToJson();
#else
        return complexProperty.IsCollection;
#endif
    }
}
