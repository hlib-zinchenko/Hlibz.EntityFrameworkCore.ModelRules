using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Internal;

/// <summary>
/// The configured rules with their exclusions, ready to run against a model.
/// </summary>
internal sealed class ModelRuleSet(
    IReadOnlyList<(IModelRule Rule, ModelRuleExclusions Exclusions)> rules,
    ModelRuleExclusions globalExclusions)
{
    public IReadOnlyList<ModelRuleViolation> Validate(IReadOnlyModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<ModelRuleViolation> violations = [];
        foreach ((IModelRule rule, ModelRuleExclusions exclusions) in rules)
        {
            violations.AddRange(
                rule.Validate(model)
                    .Where(violation => !exclusions.Matches(violation)
                                        && !globalExclusions.Matches(violation)));
        }

        return violations;
    }

    public void Enforce(IReadOnlyModel model)
    {
        IReadOnlyList<ModelRuleViolation> violations = Validate(model);
        if (violations.Count > 0)
        {
            throw new ModelRuleViolationException(violations);
        }
    }
}
