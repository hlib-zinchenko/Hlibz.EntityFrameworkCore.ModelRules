using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Tests;

/// <summary>
/// A context whose model is whatever the test configures. Each instance builds its own model
/// (EF otherwise caches one model per context type), and building a model never opens the
/// connection, so no database is needed.
/// </summary>
/// <param name="configureModel">Builds the model.</param>
/// <param name="configureConventions">Optional pre-convention configuration.</param>
/// <param name="prebuiltModel">
/// Optional ready-made model passed to <c>UseModel</c>, standing in for a compiled model.
/// </param>
/// <param name="provider">The EF Core provider to build the model with.</param>
internal sealed class TestDbContext(
    Action<ModelBuilder> configureModel,
    Action<ModelConfigurationBuilder>? configureConventions = null,
    IModel? prebuiltModel = null,
    TestProvider provider = TestProvider.Npgsql) : DbContext
{
    private const string ConnectionString = "Server=localhost;Database=model_rules_tests";

    public static IReadOnlyList<ModelRuleViolation> Validate(
        Action<ModelBuilder> configureModel,
        Action<ModelRulesBuilder> configureRules,
        Action<ModelConfigurationBuilder>? configureConventions = null,
        TestProvider provider = TestProvider.Npgsql)
    {
        using TestDbContext context = new(configureModel, configureConventions, provider: provider);
        return ModelRules.Validate(context, configureRules);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        _ = provider switch
        {
            TestProvider.Npgsql => optionsBuilder.UseNpgsql(ConnectionString),
            TestProvider.SqlServer => optionsBuilder.UseSqlServer(ConnectionString),
            TestProvider.Sqlite => optionsBuilder.UseSqlite("Data Source=:memory:"),
            TestProvider.MySql => optionsBuilder.UseMySQL(ConnectionString),
#if !NET10_0_OR_GREATER
            // An explicit server version, so the provider never connects to detect one.
            TestProvider.Pomelo => optionsBuilder.UseMySql(
                ConnectionString,
                new MySqlServerVersion(new Version(8, 0, 36))),
#endif
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
        };

        optionsBuilder.ReplaceService<IModelCacheKeyFactory, PerInstanceModelCacheKeyFactory>();

        if (prebuiltModel is not null)
        {
            optionsBuilder.UseModel(prebuiltModel);
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configureConventions?.Invoke(configurationBuilder);

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        configureModel(modelBuilder);

    private sealed class PerInstanceModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) =>
            (context.ContextId.InstanceId, designTime);
    }
}
