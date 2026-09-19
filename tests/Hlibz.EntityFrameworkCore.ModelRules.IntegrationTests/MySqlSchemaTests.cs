using Hlibz.EntityFrameworkCore.ModelRules.IntegrationTests.Databases;

namespace Hlibz.EntityFrameworkCore.ModelRules.IntegrationTests;

public sealed class MySqlSchemaTests(MySqlFixture database)
    : DatabaseSchemaTests<MySqlFixture>(database), IClassFixture<MySqlFixture>
{
    [Fact]
    public async Task NamesFollow_WithPrimaryKeyName_ChecksNameMySqlReplacesWithPrimary()
    {
        await using IntegrationDbContext context = Database.CreateContext(Models.Clean);

        await context.Database.EnsureCreatedAsync(CancellationToken);

        IReadOnlyList<(string Kind, string Name)> identifiers =
            await Database.GetIdentifiersAsync(context);
        Assert.Contains(("constraint", "PRIMARY"), identifiers);
        Assert.DoesNotContain(identifiers, identifier => identifier.Name == "pk_blogs");
    }
}
