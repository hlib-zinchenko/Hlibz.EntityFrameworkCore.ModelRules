using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Internal;

/// <summary>
/// A database identifier the model produces, with what a violation on it is reported against.
/// </summary>
/// <param name="Scope">The kind of identifier.</param>
/// <param name="Kind">A human-readable kind, e.g. <c>column</c> or <c>foreign key</c>.</param>
/// <param name="Name">The identifier itself.</param>
/// <param name="EntityType">
/// The entity type a violation on the identifier is reported against, or <see langword="null"/>
/// for an identifier that doesn't belong to one (a sequence, a function, or a schema only they
/// use).
/// </param>
/// <param name="MemberPath">The member the identifier comes from, if any.</param>
/// <param name="Schema">
/// For a table, view, sequence or function, the schema it lives in.
/// </param>
internal sealed record ModelIdentifier(
    NamingScope Scope,
    string Kind,
    string Name,
    IReadOnlyEntityType? EntityType,
    string? MemberPath,
    string? Schema = null)
{
    /// <summary>
    /// The target of a violation on an identifier with no entity type, e.g.
    /// <c>sequence sales.order_numbers</c>.
    /// </summary>
    public string Target => Schema is null ? $"{Kind} {Name}" : $"{Kind} {Schema}.{Name}";
}

/// <summary>
/// Collects every database identifier the model produces - schemas, tables, views, columns
/// (complex-type and JSON container columns included), key, foreign key, index and check
/// constraint names, sequences and database functions - each exactly once, even when TPH, table
/// splitting or owned types map several entity types to the same table.
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

        void AddSchema(string? schema, IReadOnlyEntityType? entityType)
        {
            if (schema is not null)
            {
                Add(
                    new ModelIdentifier(NamingScope.Schemas, "schema", schema, entityType, null),
                    string.Empty);
            }
        }

        // Base and owner types first, so an identifier several entity types share (a TPH column,
        // a table-splitting key) is reported against the type that introduces it, whatever order
        // the model lists them in.
        foreach (IReadOnlyEntityType entityType in model.GetEntityTypes().OrderBy(MappingDepth))
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
                        store);
                    AddSchema(storeObject.Schema, entityType);
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

            if (StoreObjectIdentifier.Create(entityType, StoreObjectType.Table) is { } table)
            {
                AddConstraintNames(entityType, table, Add);
            }
        }

        foreach (IReadOnlySequence sequence in model.GetSequences())
        {
            Add(
                new ModelIdentifier(
                    NamingScope.Sequences,
                    "sequence",
                    sequence.Name,
                    null,
                    null,
                    sequence.Schema),
                sequence.Schema ?? string.Empty);
            AddSchema(sequence.Schema, null);
        }

        foreach (IReadOnlyDbFunction function in model.GetDbFunctions())
        {
            if (function.IsBuiltIn)
            {
                continue;
            }

            // Overloads of one database function share its name, so a function is keyed by its
            // schema-qualified name, not by the CLR method.
            Add(
                new ModelIdentifier(
                    NamingScope.Functions,
                    "function",
                    function.Name,
                    null,
                    null,
                    function.Schema),
                function.Schema ?? string.Empty);
            AddSchema(function.Schema, null);
        }

        return identifiers;
    }

    /// <summary>
    /// Key, foreign key, index and check constraint names on one table. Inherited ones included,
    /// because under TPC every concrete table gets its own copy with its own name.
    /// </summary>
    private static void AddConstraintNames(
        IReadOnlyEntityType entityType,
        StoreObjectIdentifier table,
        Action<ModelIdentifier, string> add)
    {
        string store = table.DisplayName();

        foreach (IReadOnlyKey key in entityType.GetKeys())
        {
            if (key.GetName(table) is { } keyName)
            {
                string kind = key.IsPrimaryKey() ? "primary key" : "alternate key";
                add(
                    new ModelIdentifier(
                        NamingScope.Keys,
                        kind,
                        keyName,
                        key.DeclaringEntityType,
                        PropertyNames(key.Properties)),
                    store);
            }
        }

        foreach (IReadOnlyForeignKey foreignKey in entityType.GetForeignKeys())
        {
            if (StoreObjectIdentifier.Create(foreignKey.PrincipalEntityType, StoreObjectType.Table)
                    is not { } principalTable
                || IsRowInternal(foreignKey, table, principalTable)
                || foreignKey.GetConstraintName(table, principalTable) is not { } constraintName)
            {
                continue;
            }

            add(
                new ModelIdentifier(
                    NamingScope.ForeignKeys,
                    "foreign key",
                    constraintName,
                    foreignKey.DeclaringEntityType,
                    foreignKey.DependentToPrincipal?.Name ?? PropertyNames(foreignKey.Properties)),
                store);
        }

        foreach (IReadOnlyIndex index in entityType.GetIndexes())
        {
            if (index.GetDatabaseName(table) is { } indexName)
            {
                add(
                    new ModelIdentifier(
                        NamingScope.Indexes,
                        "index",
                        indexName,
                        index.DeclaringEntityType,
                        PropertyNames(index.Properties)),
                    store);
            }
        }

        foreach (IReadOnlyCheckConstraint checkConstraint in entityType.GetCheckConstraints())
        {
            if (checkConstraint.GetName(table) is { Length: > 0 } checkName)
            {
                add(
                    new ModelIdentifier(
                        NamingScope.CheckConstraints,
                        "check constraint",
                        checkName,
                        checkConstraint.EntityType,
                        null),
                    store);
            }
        }
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
    /// Whether the foreign key links two entity types sharing one row (table splitting, or an
    /// owned type in its owner's table). The database has no constraint for it, although EF
    /// still derives a name.
    /// </summary>
    private static bool IsRowInternal(
        IReadOnlyForeignKey foreignKey,
        StoreObjectIdentifier table,
        StoreObjectIdentifier principalTable) =>
        table == principalTable
        && foreignKey.PrincipalKey.IsPrimaryKey()
        && !foreignKey.PrincipalEntityType.IsAssignableFrom(foreignKey.DeclaringEntityType)
        && foreignKey.DeclaringEntityType.FindPrimaryKey() is { } primaryKey
        && primaryKey.Properties.SequenceEqual(foreignKey.Properties);

    /// <summary>
    /// How many base-type and ownership links lead from the entity type to a root it is mapped
    /// under.
    /// </summary>
    private static int MappingDepth(IReadOnlyEntityType entityType)
    {
        int depth = 0;
        for (IReadOnlyEntityType? parent = Parent(entityType); parent is not null; parent = Parent(parent))
        {
            depth++;
        }

        return depth;

        static IReadOnlyEntityType? Parent(IReadOnlyEntityType type) =>
            type.BaseType ?? type.FindOwnership()?.PrincipalEntityType;
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
