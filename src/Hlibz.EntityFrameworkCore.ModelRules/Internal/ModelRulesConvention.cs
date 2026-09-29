using System.Runtime.CompilerServices;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Hlibz.EntityFrameworkCore.ModelRules.Internal;

/// <summary>
/// Applies the configured defaults, then runs the rules, while the model is being finalized.
/// </summary>
/// <remarks>
/// <para>
/// A finalizing convention rather than a finalized one: a convention added through
/// <c>ModelConfigurationBuilder.Conventions</c> is appended after EF's own conventions, and on the
/// finalized side that is after the model has already been converted to its slimmed-down runtime
/// form. While finalizing, the model is still the full design-time one - the same instance that
/// <c>IDesignTimeModel</c> later hands out - and every other convention (naming plugins, index and
/// constraint name uniquification) has already run.
/// </para>
/// <para>
/// One instance per <see cref="ModelConfigurationBuilder"/>, shared by every extension method
/// that configures it, so defaults are always applied before any rule runs, whichever order
/// <c>ConfigureConventions</c> calls them in.
/// </para>
/// </remarks>
internal sealed class ModelRulesConvention : IModelFinalizingConvention
{
    private static readonly ConditionalWeakTable<IReadOnlyModel, object> CheckedModels = [];

    private static readonly ConditionalWeakTable<ModelConfigurationBuilder, ModelRulesConvention>
        Registrations = [];

    private readonly List<ModelRuleSet> _ruleSets = [];

    private DeleteBehavior? _clientSetNullReplacement;

    private ModelRulesConvention()
    {
    }

    /// <summary>
    /// The convention registered on this builder, registering one on first use.
    /// </summary>
    public static ModelRulesConvention For(ModelConfigurationBuilder configurationBuilder)
    {
        if (!Registrations.TryGetValue(configurationBuilder, out ModelRulesConvention? convention))
        {
            convention = new ModelRulesConvention();
            Registrations.Add(configurationBuilder, convention);
            configurationBuilder.Conventions.Add(_ => convention);
        }

        return convention;
    }

    /// <summary>
    /// Whether this convention ran against the model with at least one rule set, and all its
    /// rules passed.
    /// </summary>
    public static bool HasChecked(IReadOnlyModel model) => CheckedModels.TryGetValue(model, out _);

    public void AddRuleSet(ModelRuleSet ruleSet) => _ruleSets.Add(ruleSet);

    public void ReplaceClientSetNull(DeleteBehavior replacement) =>
        _clientSetNullReplacement = replacement;

    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        if (_clientSetNullReplacement is { } replacement)
        {
            ReplaceClientSetNull(modelBuilder.Metadata, replacement);
        }

        foreach (ModelRuleSet ruleSet in _ruleSets)
        {
            ruleSet.Enforce(modelBuilder.Metadata);
        }

        if (_ruleSets.Count > 0)
        {
            CheckedModels.AddOrUpdate(modelBuilder.Metadata, _ruleSets);
        }
    }

    /// <summary>
    /// Replaces <see cref="DeleteBehavior.ClientSetNull"/> on every relationship that has it by
    /// default or by convention. A behavior configured with <c>OnDelete</c> or the
    /// <c>[DeleteBehavior]</c> attribute is left alone.
    /// </summary>
    private static void ReplaceClientSetNull(IConventionModel model, DeleteBehavior replacement)
    {
        List<IConventionForeignKey> foreignKeys = model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetDeclaredForeignKeys())
            .Where(foreignKey => !foreignKey.IsOwnership
                                 && foreignKey.DeleteBehavior == DeleteBehavior.ClientSetNull
                                 && foreignKey.GetDeleteBehaviorConfigurationSource()
                                     is null or ConfigurationSource.Convention)
            .ToList();

        foreach (IConventionForeignKey foreignKey in foreignKeys)
        {
            foreignKey.Builder.OnDelete(replacement);
        }
    }
}
