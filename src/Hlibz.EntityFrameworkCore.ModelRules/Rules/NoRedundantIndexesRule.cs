using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Rules;

/// <summary>
/// MR014: no index is made redundant by another index or key on the same table, one whose columns
/// start with the same columns in the same order. A redundant index costs every insert and update
/// and serves no lookup the other one doesn't.
/// </summary>
/// <remarks>
/// Conservative on purpose: filtered indexes and indexes with provider-specific settings
/// (clustered, included columns, a non-default index method, ...) are never compared, and a
/// unique index only counts as redundant when it duplicates another unique index or key exactly,
/// since it enforces something a longer index doesn't.
/// </remarks>
internal sealed class NoRedundantIndexesRule() : ModelRule("MR014", "NoRedundantIndexes")
{
    public override IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model)
    {
        foreach (List<TableIndex> indexes in IndexesByTable(model))
        {
            foreach (TableIndex index in indexes)
            {
                if (index.Index is null || !index.IsComparable)
                {
                    continue;
                }

                if (indexes.FirstOrDefault(other => Covers(other, index)) is not { } covering)
                {
                    continue;
                }

                string relation = covering.Columns.Count == index.Columns.Count
                    ? "has the same columns as"
                    : "is a leading prefix of";

                yield return Violation(
                    index.Index.DeclaringEntityType,
                    string.Join(", ", index.Index.Properties.Select(property => property.Name)),
                    $"index '{index.Name}' ({string.Join(", ", index.Columns)}) {relation} "
                    + $"{covering.Kind} '{covering.Name}' "
                    + $"({string.Join(", ", covering.Columns)}), which serves the same lookups. "
                    + "Remove it.");
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="other"/> makes <paramref name="index"/> redundant.
    /// </summary>
    private static bool Covers(TableIndex other, TableIndex index)
    {
        if (ReferenceEquals(other, index)
            || !other.IsComparable
            || other.Columns.Count < index.Columns.Count
            || !other.Columns.Take(index.Columns.Count).SequenceEqual(index.Columns)
            || !other.Descending.Take(index.Columns.Count).SequenceEqual(index.Descending))
        {
            return false;
        }

        bool sameColumns = other.Columns.Count == index.Columns.Count;
        if (index.IsUnique && (!sameColumns || !other.IsUnique))
        {
            return false;
        }

        // Of two identical indexes, report only one: a key always wins, then a unique index,
        // then the name that sorts first.
        if (sameColumns && other.IsUnique == index.IsUnique && other.Index is not null)
        {
            return string.CompareOrdinal(other.Name, index.Name) < 0;
        }

        return true;
    }

    /// <summary>
    /// The indexes and keys of every table, gathered across all entity types mapped to it (TPH,
    /// table splitting), each once. Under TPC each concrete table has its own.
    /// </summary>
    private static IEnumerable<List<TableIndex>> IndexesByTable(IReadOnlyModel model)
    {
        Dictionary<StoreObjectIdentifier, List<TableIndex>> tables = [];

        foreach (IReadOnlyEntityType entityType in model.GetEntityTypes())
        {
            if (StoreObjectIdentifier.Create(entityType, StoreObjectType.Table) is not { } table)
            {
                continue;
            }

            if (!tables.TryGetValue(table, out List<TableIndex>? indexes))
            {
                tables[table] = indexes = [];
            }

            foreach (IReadOnlyKey key in entityType.GetKeys())
            {
                if (key.GetName(table) is { } name
                    && Columns(key.Properties, table) is { } columns
                    && !indexes.Exists(index => index.Name == name))
                {
                    indexes.Add(new TableIndex(
                        name,
                        key.IsPrimaryKey() ? "primary key" : "alternate key",
                        columns,
                        [.. columns.Select(_ => false)],
                        IsUnique: true,
                        IsComparable: true,
                        Index: null));
                }
            }

            foreach (IReadOnlyIndex index in entityType.GetIndexes())
            {
                if (index.GetDatabaseName(table) is { } name
                    && Columns(index.Properties, table) is { } columns
                    && !indexes.Exists(existing => existing.Name == name))
                {
                    indexes.Add(new TableIndex(
                        name,
                        index.IsUnique ? "unique index" : "index",
                        columns,
                        Descending(index, columns.Count),
                        index.IsUnique,
                        IsComparable(index),
                        index));
                }
            }
        }

        return tables.Values;
    }

    private static List<string>? Columns(
        IReadOnlyList<IReadOnlyProperty> properties,
        StoreObjectIdentifier table)
    {
        List<string> columns = [];
        foreach (IReadOnlyProperty property in properties)
        {
            if (property.GetColumnName(table) is not { } column)
            {
                return null;
            }

            columns.Add(column);
        }

        return columns;
    }

    /// <summary>
    /// Per-column sort order. EF Core stores <see langword="null"/> for all ascending and an
    /// empty list for all descending.
    /// </summary>
    private static List<bool> Descending(IReadOnlyIndex index, int count) =>
        index.IsDescending switch
        {
            null => [.. Enumerable.Repeat(false, count)],
            { Count: 0 } => [.. Enumerable.Repeat(true, count)],
            { } descending => [.. descending],
        };

    /// <summary>
    /// Whether the index is a plain one: no filter, and nothing provider-specific (clustered,
    /// included columns, an index method, ...) that could make it serve lookups another index
    /// doesn't.
    /// </summary>
    private static bool IsComparable(IReadOnlyIndex index) =>
        index.GetFilter() is null
        && index.GetAnnotations().All(annotation =>
            annotation.Name.StartsWith("Relational:", StringComparison.Ordinal));

    /// <param name="Name">The index or key name.</param>
    /// <param name="Kind">What to call it in a message, e.g. <c>primary key</c>.</param>
    /// <param name="Columns">The column names, in order.</param>
    /// <param name="Descending">Per column, whether it's sorted descending.</param>
    /// <param name="IsUnique">Whether the index (or key) enforces uniqueness.</param>
    /// <param name="IsComparable">Whether it may be compared with others at all.</param>
    /// <param name="Index">The index, or <see langword="null"/> for a key.</param>
    private sealed record TableIndex(
        string Name,
        string Kind,
        List<string> Columns,
        List<bool> Descending,
        bool IsUnique,
        bool IsComparable,
        IReadOnlyIndex? Index);
}
