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
