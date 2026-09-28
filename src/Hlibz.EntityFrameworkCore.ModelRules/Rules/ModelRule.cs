using Hlibz.EntityFrameworkCore.ModelRules.Internal;

using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Rules;

/// <summary>
/// Base class for the built-in rules: holds the id/name pair and builds violations.
/// </summary>
internal abstract class ModelRule(string id, string name) : IModelRule
{
    public string Id { get; } = id;

    public string Name { get; } = name;

    public abstract IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model);

    protected ModelRuleViolation Violation(
        IReadOnlyEntityType entityType,
        string? memberPath,
        string message) =>
        new(this, entityType, memberPath, message);

    /// <summary>
    /// A violation on a database identifier: against its entity type when it belongs to one,
    /// otherwise (sequences, functions, their schemas) against the identifier itself.
    /// </summary>
    protected ModelRuleViolation Violation(ModelIdentifier identifier, string message) =>
        identifier.EntityType is { } entityType
            ? new(this, entityType, identifier.MemberPath, message)
            : new(this, identifier.Target, message);
}
