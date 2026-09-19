using Hlibz.EntityFrameworkCore.ModelRules.IntegrationTests.Databases;

namespace Hlibz.EntityFrameworkCore.ModelRules.IntegrationTests;

public sealed class SqlServerSchemaTests(SqlServerFixture database)
    : DatabaseSchemaTests<SqlServerFixture>(database), IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task DecimalsHavePrecision_WithoutPrecision_CreatesDecimal18Scale2()
    {
        await using IntegrationDbContext context = Database.CreateContext(Models.Dogs);

        IReadOnlyList<ModelRuleViolation> violations =
            ModelRules.Validate(context, rules => rules.DecimalsHavePrecision());
        await context.Database.EnsureCreatedAsync(CancellationToken);

        Assert.Equal("Dog.Weight", Assert.Single(violations).Target);
        object?[] column = Assert.Single(await DatabaseFixture.QueryAsync(
            context,
            """
            select DATA_TYPE, cast(NUMERIC_PRECISION as int), NUMERIC_SCALE
            from INFORMATION_SCHEMA.COLUMNS where COLUMN_NAME = 'Weight'
            """));
        Assert.Equal(["decimal", 18, 2], column);
    }

    [Fact]
    public async Task NoShadowProperties_WithTemporalTable_AllowsRealPeriodColumns()
    {
        await using IntegrationDbContext context = Database.CreateContext(
            model => model.Entity<State>().ToTable("states", table => table.IsTemporal()),
            rules => rules.NoShadowProperties());

        await context.Database.EnsureCreatedAsync(CancellationToken);

        IReadOnlyList<(string Kind, string Name)> identifiers =
            await Database.GetIdentifiersAsync(context);
        Assert.Contains(("column", "PeriodStart"), identifiers);
        Assert.Contains(("column", "PeriodEnd"), identifiers);
    }
}
