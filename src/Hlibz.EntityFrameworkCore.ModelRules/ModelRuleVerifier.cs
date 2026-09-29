using System.Reflection;

using Hlibz.EntityFrameworkCore.ModelRules.Internal;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules;

/// <summary>
/// Runs model rules outside the model-building pipeline - typically from a unit test.
/// </summary>
public static class ModelRuleVerifier
{
    /// <summary>
    /// Builds the context's full design-time model, which runs the rules registered with
    /// <c>UseModelRules</c> in its <c>ConfigureConventions</c>, and throws if any are broken.
    /// Works for contexts that use a compiled model at runtime, where the rules are otherwise never
    /// run.
    /// </summary>
    /// <param name="context">The context whose model to check.</param>
    /// <exception cref="ModelRuleViolationException">The model breaks a rule.</exception>
    /// <exception cref="InvalidOperationException">
    /// The context has no rules registered, so there was nothing to verify.
    /// </exception>
    public static void Verify(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IModel model = GetDesignTimeModel(context);
        if (!ModelRulesConvention.HasChecked(model))
        {
            throw new InvalidOperationException(
                $"{context.GetType().Name} has no model rules registered. Call "
                + "configurationBuilder.UseModelRules(...) in its ConfigureConventions, or pass "
                + "the rules to ModelRuleVerifier.Verify(context, rules => ...).");
        }
    }

    /// <summary>
    /// Creates the context the options were built for, then checks it as
    /// <see cref="Verify(DbContext)"/> does. Saves building the context by hand in a test,
    /// including in a theory over several contexts.
    /// </summary>
    /// <param name="options">Options built with <c>DbContextOptionsBuilder&lt;TContext&gt;</c>.
    /// The context must have a public constructor that takes only them.</param>
    /// <exception cref="ModelRuleViolationException">The model breaks a rule.</exception>
    /// <exception cref="InvalidOperationException">
    /// The context has no rules registered, so there was nothing to verify.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The options don't name a context type that can be created from them.
    /// </exception>
    public static void Verify(DbContextOptions options)
    {
        using DbContext context = CreateContext(options);
        Verify(context);
    }

    /// <summary>
    /// Checks the context's full design-time model against the given rules, independently of any
    /// rules registered on the context itself, and throws if any are broken.
    /// </summary>
    /// <param name="context">The context whose model to check.</param>
    /// <param name="configure">Chooses the rules to check.</param>
    /// <exception cref="ModelRuleViolationException">The model breaks a rule.</exception>
    public static void Verify(DbContext context, Action<ModelRulesBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(context);
        ModelRulesBuilder.Build(configure).Enforce(GetDesignTimeModel(context));
    }

    /// <summary>
    /// Creates the context the options were built for, then checks its full design-time model
    /// against the given rules, independently of any rules registered on the context itself.
    /// </summary>
    /// <param name="options">Options built with <c>DbContextOptionsBuilder&lt;TContext&gt;</c>.
    /// The context must have a public constructor that takes only them.</param>
    /// <param name="configure">Chooses the rules to check.</param>
    /// <exception cref="ModelRuleViolationException">The model breaks a rule.</exception>
    /// <exception cref="ArgumentException">
    /// The options don't name a context type that can be created from them.
    /// </exception>
    public static void Verify(DbContextOptions options, Action<ModelRulesBuilder> configure)
    {
        using DbContext context = CreateContext(options);
        Verify(context, configure);
    }

    /// <summary>
    /// Checks a model against the given rules and throws if any are broken.
    /// </summary>
    /// <param name="model">The model to check - use the design-time model
    /// (<c>context.GetService&lt;IDesignTimeModel&gt;().Model</c>), since the runtime
    /// <c>context.Model</c> no longer carries all the metadata the rules read.</param>
    /// <param name="configure">Chooses the rules to check.</param>
    /// <exception cref="ModelRuleViolationException">The model breaks a rule.</exception>
    public static void Verify(IReadOnlyModel model, Action<ModelRulesBuilder> configure) =>
        ModelRulesBuilder.Build(configure).Enforce(model);

    /// <summary>
    /// Checks the context's full design-time model against the given rules and returns every
    /// violation instead of throwing.
    /// </summary>
    /// <param name="context">The context whose model to check.</param>
    /// <param name="configure">Chooses the rules to check.</param>
    /// <returns>Every violation found, in rule registration order; empty when the model passes.</returns>
    public static IReadOnlyList<ModelRuleViolation> Validate(
        DbContext context,
        Action<ModelRulesBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ModelRulesBuilder.Build(configure).Validate(GetDesignTimeModel(context));
    }

    /// <summary>
    /// Creates the context the options were built for, then checks its full design-time model
    /// against the given rules and returns every violation instead of throwing.
    /// </summary>
    /// <param name="options">Options built with <c>DbContextOptionsBuilder&lt;TContext&gt;</c>.
    /// The context must have a public constructor that takes only them.</param>
    /// <param name="configure">Chooses the rules to check.</param>
    /// <returns>Every violation found, in rule registration order; empty when the model passes.</returns>
    /// <exception cref="ArgumentException">
    /// The options don't name a context type that can be created from them.
    /// </exception>
    public static IReadOnlyList<ModelRuleViolation> Validate(
        DbContextOptions options,
        Action<ModelRulesBuilder> configure)
    {
        using DbContext context = CreateContext(options);
        return Validate(context, configure);
    }

    /// <summary>
    /// Checks a model against the given rules and returns every violation instead of throwing.
    /// </summary>
    /// <param name="model">The model to check - use the design-time model
    /// (<c>context.GetService&lt;IDesignTimeModel&gt;().Model</c>), since the runtime
    /// <c>context.Model</c> no longer carries all the metadata the rules read.</param>
    /// <param name="configure">Chooses the rules to check.</param>
    /// <returns>Every violation found, in rule registration order; empty when the model passes.</returns>
    public static IReadOnlyList<ModelRuleViolation> Validate(
        IReadOnlyModel model,
        Action<ModelRulesBuilder> configure) =>
        ModelRulesBuilder.Build(configure).Validate(model);

    private static DbContext CreateContext(DbContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Type contextType = options.ContextType;
        if (contextType == typeof(DbContext) || contextType.IsAbstract)
        {
            throw new ArgumentException(
                $"The options are for {contextType.Name}, not a concrete context. Build them with "
                + "DbContextOptionsBuilder<TContext>.",
                nameof(options));
        }

        ConstructorInfo constructor =
            contextType.GetConstructor([typeof(DbContextOptions<>).MakeGenericType(contextType)])
            ?? contextType.GetConstructor([typeof(DbContextOptions)])
            ?? throw new ArgumentException(
                $"{contextType.Name} has no public constructor that takes only its "
                + "DbContextOptions. Create the context yourself and pass it to Verify instead.",
                nameof(options));

        return (DbContext)constructor.Invoke(
            BindingFlags.DoNotWrapExceptions,
            binder: null,
            [options],
            culture: null);
    }

    private static IModel GetDesignTimeModel(DbContext context) =>
        context.GetService<IDesignTimeModel>().Model;
}
