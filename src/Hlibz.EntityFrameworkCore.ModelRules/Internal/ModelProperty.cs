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
