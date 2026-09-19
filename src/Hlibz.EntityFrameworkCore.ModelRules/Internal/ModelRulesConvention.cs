using System.Runtime.CompilerServices;

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Hlibz.EntityFrameworkCore.ModelRules.Internal;

/// <summary>
/// Runs the rules while the model is being finalized.
/// </summary>
/// <remarks>
/// A finalizing convention rather than a finalized one: a convention added through
/// <c>ModelConfigurationBuilder.Conventions</c> is appended after EF's own conventions, and on the
/// finalized side that is after the model has already been converted to its slimmed-down runtime
/// form. While finalizing, the model is still the full design-time one - the same instance that
/// <c>IDesignTimeModel</c> later hands out - and every other convention (naming plugins, index and
/// constraint name uniquification) has already run.
/// </remarks>
internal sealed class ModelRulesConvention(ModelRuleSet ruleSet) : IModelFinalizingConvention
{
    private static readonly ConditionalWeakTable<IReadOnlyModel, object> CheckedModels = [];

    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        ruleSet.Enforce(modelBuilder.Metadata);
        CheckedModels.AddOrUpdate(modelBuilder.Metadata, ruleSet);
    }

    /// <summary>
    /// Whether this convention ran against the model and all its rules passed.
    /// </summary>
    public static bool HasChecked(IReadOnlyModel model) => CheckedModels.TryGetValue(model, out _);
}
