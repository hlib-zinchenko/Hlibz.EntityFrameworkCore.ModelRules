using Hlibz.EntityFrameworkCore.ModelRules;
using Hlibz.EntityFrameworkCore.ModelRules.Internal;

// Lives in EF Core's own namespace so UseModelRules shows up in ConfigureConventions without an
// extra using, the same way provider and plugin extensions do.
namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Registers model rules on a <see cref="DbContext"/>.
/// </summary>
public static class ModelConfigurationBuilderExtensions
{
    /// <summary>
    /// Enforces the given rules every time EF Core builds this context's model: once per process
    /// at runtime (on first use of the context), and whenever tooling such as
    /// <c>dotnet ef migrations add</c> builds it. A broken rule throws
    /// <see cref="ModelRuleViolationException"/> listing every violation.
    /// </summary>
    /// <remarks>
    /// A context using a compiled model skips model building - and so these rules - at runtime.
    /// Call <see cref="ModelRuleVerifier.Verify(DbContext)"/> from a test to keep it covered.
    /// </remarks>
    /// <param name="configurationBuilder">The builder passed to <c>ConfigureConventions</c>.</param>
    /// <param name="configure">Chooses the rules to enforce.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static ModelConfigurationBuilder UseModelRules(
        this ModelConfigurationBuilder configurationBuilder,
        Action<ModelRulesBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        ModelRuleSet ruleSet = ModelRulesBuilder.Build(configure);
        ModelRulesConvention.For(configurationBuilder).AddRuleSet(ruleSet);
        return configurationBuilder;
    }

    /// <summary>
    /// Replaces <see cref="DeleteBehavior.ClientSetNull"/>, EF Core's default for optional
    /// relationships, with a delete behavior the database enforces, on every relationship whose
    /// delete behavior isn't configured explicitly. Fixes MR011
    /// (<c>NoClientSideDeleteBehaviors</c>) for every optional relationship at once.
    /// </summary>
    /// <remarks>
    /// Runs after <c>OnModelCreating</c> and every EF Core convention, and before the rules
    /// registered with <see cref="UseModelRules"/>, whichever is called first. A relationship
    /// configured with <c>OnDelete(...)</c> or the <c>[DeleteBehavior]</c> attribute keeps its
    /// behavior, <c>ClientSetNull</c> included. SQL Server rejects
    /// <see cref="DeleteBehavior.SetNull"/> where it would create multiple cascade paths; use
    /// <see cref="DeleteBehavior.Restrict"/> there, or configure those relationships explicitly.
    /// </remarks>
    /// <param name="configurationBuilder">The builder passed to <c>ConfigureConventions</c>.</param>
    /// <param name="deleteBehavior">The behavior to use instead, usually
    /// <see cref="DeleteBehavior.SetNull"/> or <see cref="DeleteBehavior.Restrict"/>.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="deleteBehavior"/> is <see cref="DeleteBehavior.ClientSetNull"/> or
    /// <see cref="DeleteBehavior.ClientCascade"/>, which MR011 reports, or isn't a defined value.
    /// </exception>
    public static ModelConfigurationBuilder ConfigureClientSetNullAs(
        this ModelConfigurationBuilder configurationBuilder,
        DeleteBehavior deleteBehavior)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        if (deleteBehavior is DeleteBehavior.ClientSetNull or DeleteBehavior.ClientCascade
            || !Enum.IsDefined(deleteBehavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(deleteBehavior),
                deleteBehavior,
                "Use a delete behavior the database enforces, such as SetNull or Restrict.");
        }

        ModelRulesConvention.For(configurationBuilder).ReplaceClientSetNull(deleteBehavior);
        return configurationBuilder;
    }
}
