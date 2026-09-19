using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Hlibz.EntityFrameworkCore.ModelRules.IntegrationTests.Databases;

/// <summary>
/// A context pointed at a real database, with whatever model and rules the test configures. Each
/// instance builds its own model.
/// </summary>
internal sealed class IntegrationDbContext(
    Action<DbContextOptionsBuilder> useProvider,
    Action<ModelBuilder> configureModel,
    Action<ModelRulesBuilder>? rules) : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        useProvider(optionsBuilder);
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, PerInstanceModelCacheKeyFactory>();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        if (rules is not null)
        {
            configurationBuilder.UseModelRules(rules);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        configureModel(modelBuilder);

    private sealed class PerInstanceModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) =>
            (context.ContextId.InstanceId, designTime);
    }
}
