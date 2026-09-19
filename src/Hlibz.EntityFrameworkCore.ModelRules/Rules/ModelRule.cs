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
}
