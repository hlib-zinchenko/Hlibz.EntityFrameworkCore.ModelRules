using Hlibz.EntityFrameworkCore.ModelRules.IntegrationTests.Databases;

namespace Hlibz.EntityFrameworkCore.ModelRules.IntegrationTests;

public sealed class PostgresSchemaTests(PostgresFixture database)
    : DatabaseSchemaTests<PostgresFixture>(database), IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task DecimalsHavePrecision_WithoutPrecision_CreatesUnconstrainedNumeric()
    {
        await using IntegrationDbContext context = Database.CreateContext(Models.Dogs);

        IReadOnlyList<ModelRuleViolation> violations =
            ModelRules.Validate(context, rules => rules.DecimalsHavePrecision());
        await context.Database.EnsureCreatedAsync(CancellationToken);

        Assert.Equal("Dog.Weight", Assert.Single(violations).Target);
        object?[] column = Assert.Single(await DatabaseFixture.QueryAsync(
            context,
            """
            select data_type::text, numeric_precision, numeric_scale
            from information_schema.columns where column_name = 'Weight'
            """));
        Assert.Equal(["numeric", null, null], column);
    }

    [Fact]
    public async Task MaxIdentifierLength_WithNameOverLimit_ReportsNamePostgresTruncates()
    {
        string tableName = "states_" + new string('x', 63);

        await using IntegrationDbContext context =
            Database.CreateContext(model => model.Entity<State>().ToTable(tableName));

        IReadOnlyList<ModelRuleViolation> violations =
            ModelRules.Validate(context, rules => rules.MaxIdentifierLength(63));
        await context.Database.EnsureCreatedAsync(CancellationToken);

        Assert.StartsWith(
            $"table name '{tableName}' is 70 characters long",
            Assert.Single(violations).Message,
            StringComparison.Ordinal);

        IReadOnlyList<(string Kind, string Name)> identifiers =
            await Database.GetIdentifiersAsync(context);
        Assert.Contains(("table", tableName[..63]), identifiers);
        Assert.DoesNotContain(("table", tableName), identifiers);
    }
}
