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
    /// Call <see cref="ModelRules.Verify(DbContext)"/> from a test to keep it covered.
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
        configurationBuilder.Conventions.Add(_ => new ModelRulesConvention(ruleSet));
        return configurationBuilder;
    }
}
