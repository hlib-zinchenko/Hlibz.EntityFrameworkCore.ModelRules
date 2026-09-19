using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules;

/// <summary>
/// A single check run against a finished EF Core model. Implement this to add a custom rule and
/// register it with <see cref="ModelRulesBuilder.Add"/>.
/// </summary>
/// <remarks>
/// Rules receive an <see cref="IReadOnlyModel"/> because they run in two places: inside model
/// finalization (where the model is still an <c>IConventionModel</c>) and from tests against the
/// design-time <see cref="IModel"/>. Only read-only metadata APIs are available in both.
/// </remarks>
public interface IModelRule
{
    /// <summary>
    /// Gets the stable identifier reported with every violation, e.g. <c>MR003</c>. Custom rules
    /// should use their own prefix so they never collide with built-in ones.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the human-readable rule name reported with every violation, e.g.
    /// <c>DecimalsHavePrecision</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Checks the model and returns every violation found. Exclusions are applied by the caller,
    /// so a rule reports everything it sees.
    /// </summary>
    /// <param name="model">The model to check.</param>
    /// <returns>The violations found, or an empty sequence.</returns>
    IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model);
}
