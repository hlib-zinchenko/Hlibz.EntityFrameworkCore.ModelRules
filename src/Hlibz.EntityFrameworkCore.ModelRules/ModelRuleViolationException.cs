using System.Text;

namespace Hlibz.EntityFrameworkCore.ModelRules;

/// <summary>
/// Thrown when an EF Core model breaks one or more model rules. Carries every violation found,
/// not just the first.
/// </summary>
public sealed class ModelRuleViolationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModelRuleViolationException"/> class.
    /// </summary>
    /// <param name="violations">The violations found. Must not be empty.</param>
    public ModelRuleViolationException(IReadOnlyList<ModelRuleViolation> violations)
        : base(BuildMessage(violations))
    {
        Violations = violations;
    }

    /// <summary>
    /// Initializes a new instance for the violations of several contexts, grouped by context.
    /// </summary>
    internal ModelRuleViolationException(
        IReadOnlyList<(Type ContextType, IReadOnlyList<ModelRuleViolation> Violations)> failures,
        int contextCount)
        : base(BuildMessage(failures, contextCount))
    {
        Violations = failures.SelectMany(failure => failure.Violations).ToList();
    }

    /// <summary>
    /// Gets every violation found, in the order the rules were registered.
    /// </summary>
    public IReadOnlyList<ModelRuleViolation> Violations { get; }

    private static string BuildMessage(IReadOnlyList<ModelRuleViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(violations);
        if (violations.Count == 0)
        {
            throw new ArgumentException("At least one violation is required.", nameof(violations));
        }

        StringBuilder message = new();
        message.Append("The EF Core model has ")
            .Append(violations.Count)
            .Append(violations.Count == 1 ? " model rule violation:" : " model rule violations:");

        foreach (ModelRuleViolation violation in violations)
        {
            message.AppendLine().Append("  - ").Append(violation);
        }

        return message.ToString();
    }

    private static string BuildMessage(
        IReadOnlyList<(Type ContextType, IReadOnlyList<ModelRuleViolation> Violations)> failures,
        int contextCount)
    {
        StringBuilder message = new();
        message.Append(failures.Count)
            .Append(" of ")
            .Append(contextCount)
            .Append(contextCount == 1 ? " EF Core model has" : " EF Core models have")
            .Append(" model rule violations:");

        foreach ((Type contextType, IReadOnlyList<ModelRuleViolation> violations) in failures)
        {
            message.AppendLine()
                .Append(contextType.Name)
                .Append(" has ")
                .Append(violations.Count)
                .Append(violations.Count == 1 ? " violation:" : " violations:");

            foreach (ModelRuleViolation violation in violations)
            {
                message.AppendLine().Append("  - ").Append(violation);
            }
        }

        return message.ToString();
    }
}
