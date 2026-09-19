using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Internal;

/// <summary>
/// A database identifier the model produces, with the entity type (and member) it is reported
/// against.
/// </summary>
/// <param name="Scope">The kind of identifier.</param>
/// <param name="Kind">A human-readable kind, e.g. <c>column</c> or <c>foreign key</c>.</param>
/// <param name="Name">The identifier itself.</param>
/// <param name="EntityType">The entity type a violation on the identifier is reported against.</param>
/// <param name="MemberPath">The member the identifier comes from, if any.</param>
/// <param name="Schema">For a table or view, the schema it lives in.</param>
internal sealed record ModelIdentifier(
    NamingScope Scope,
    string Kind,
    string Name,
    IReadOnlyEntityType EntityType,
    string? MemberPath,
    string? Schema = null);

/// <summary>
/// Collects every database identifier the model produces - schemas, tables, views, columns
/// (complex-type and JSON container columns included) and key/foreign key/index names - each
/// exactly once, even when TPH, table splitting or owned types map several entity types to the
/// same table.
/// </summary>
internal static class ModelIdentifiers
{
    private static readonly StoreObjectType[] TableLikeStoreObjectTypes =
        [StoreObjectType.Table, StoreObjectType.View];

    public static IEnumerable<ModelIdentifier> Collect(IReadOnlyModel model)
    {
        HashSet<(NamingScope Scope, string Store, string Name)> seen = [];
        List<ModelIdentifier> identifiers = [];

        void Add(ModelIdentifier identifier, string store)
        {
            if (seen.Add((identifier.Scope, store, identifier.Name)))
            {
                identifiers.Add(identifier);
            }
        }

        foreach (IReadOnlyEntityType entityType in model.GetEntityTypes())
        {
            if (entityType.IsMappedToJson())
            {
                AddJsonContainerColumn(entityType, Add);
                continue;
            }

            foreach (StoreObjectType storeObjectType in TableLikeStoreObjectTypes)
            {
                if (StoreObjectIdentifier.Create(entityType, storeObjectType) is not { } storeObject)
                {
                    continue;
                }

                string store = storeObject.DisplayName();

                if (IntroducesMapping(entityType, storeObject))
                {
                    string kind = storeObjectType == StoreObjectType.Table ? "table" : "view";
                    Add(
                        new ModelIdentifier(
                            NamingScope.Tables,
                            kind,
                            storeObject.Name,
                            entityType,
                            null,
                            storeObject.Schema),
                        string.Empty);

                    if (storeObject.Schema is { } schema)
                    {
                        Add(
                            new ModelIdentifier(NamingScope.Schemas, "schema", schema, entityType, null),
                            string.Empty);
                    }
                }

                foreach (ModelProperty property in ModelWalker.AllProperties(entityType))
                {
                    if (!property.IsJson && property.Property.GetColumnName(storeObject) is { } column)
                    {
                        Add(
                            new ModelIdentifier(
                                NamingScope.Columns,
                                "column",
                                column,
                                property.EntityType,
                                property.Path),
                            store);
                    }
                }

#if NET10_0_OR_GREATER
                foreach (IReadOnlyComplexProperty complexProperty in entityType.GetComplexProperties())
                {
                    if (complexProperty.ComplexType.IsMappedToJson()
                        && complexProperty.ComplexType.GetContainerColumnName() is { } container)
                    {
                        Add(
                            new ModelIdentifier(
                                NamingScope.Columns,
                                "column",
                                container,
                                (IReadOnlyEntityType)complexProperty.DeclaringType,
                                complexProperty.Name),
                            store);
                    }
                }
#endif
            }

            if (StoreObjectIdentifier.Create(entityType, StoreObjectType.Table) is not { } table)
            {
                continue;
            }

            string tableStore = table.DisplayName();

            foreach (IReadOnlyKey key in entityType.GetKeys())
            {
                if (key.GetName(table) is { } keyName)
                {
                    string kind = key.IsPrimaryKey() ? "primary key" : "alternate key";
                    Add(
                        new ModelIdentifier(
                            NamingScope.Keys,
                            kind,
                            keyName,
                            key.DeclaringEntityType,
                            PropertyNames(key.Properties)),
                        tableStore);
                }
            }

            foreach (IReadOnlyForeignKey foreignKey in entityType.GetDeclaredForeignKeys())
            {
                if (foreignKey.GetConstraintName() is { } constraintName)
                {
                    Add(
                        new ModelIdentifier(
                            NamingScope.ForeignKeys,
                            "foreign key",
                            constraintName,
                            entityType,
                            foreignKey.DependentToPrincipal?.Name
                            ?? PropertyNames(foreignKey.Properties)),
                        tableStore);
                }
            }

            foreach (IReadOnlyIndex index in entityType.GetDeclaredIndexes())
            {
                if (index.GetDatabaseName() is { } indexName)
                {
                    Add(
                        new ModelIdentifier(
                            NamingScope.Indexes,
                            "index",
                            indexName,
                            entityType,
                            PropertyNames(index.Properties)),
                        tableStore);
                }
            }
        }

        return identifiers;
    }

    /// <summary>
    /// Whether this entity type is the one to report its table/view name against: not a TPH-derived
    /// type sharing its base type's table, and not an owned type sharing its owner's table.
    /// </summary>
    private static bool IntroducesMapping(
        IReadOnlyEntityType entityType,
        StoreObjectIdentifier storeObject)
    {
        if (entityType.BaseType is { } baseType
            && StoreObjectIdentifier.Create(baseType, storeObject.StoreObjectType) == storeObject)
        {
            return false;
        }

        return entityType.FindOwnership() is not { } ownership
               || StoreObjectIdentifier.Create(
                   ownership.PrincipalEntityType,
                   storeObject.StoreObjectType) != storeObject;
    }

    /// <summary>
    /// A JSON-mapped owned type has no columns of its own; only the root of the JSON document
    /// contributes one identifier - the container column on its owner's table.
    /// </summary>
    private static void AddJsonContainerColumn(
        IReadOnlyEntityType entityType,
        Action<ModelIdentifier, string> add)
    {
        if (entityType.FindOwnership() is not { } ownership
            || ownership.PrincipalEntityType.IsMappedToJson()
            || entityType.GetContainerColumnName() is not { } container
            || StoreObjectIdentifier.Create(ownership.PrincipalEntityType, StoreObjectType.Table)
                is not { } table)
        {
            return;
        }

        add(
            new ModelIdentifier(
                NamingScope.Columns,
                "column",
                container,
                ownership.PrincipalEntityType,
                ownership.PrincipalToDependent?.Name),
            table.DisplayName());
    }

    private static string PropertyNames(IReadOnlyList<IReadOnlyProperty> properties) =>
        string.Join(", ", properties.Select(property => property.Name));
}
