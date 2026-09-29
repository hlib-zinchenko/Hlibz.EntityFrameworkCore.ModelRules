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

    [Fact]
    public async Task ToJson_WithOracleProvider_ChecksModelButCannotCreateTable()
    {
        // Why the shared JSON tests are skipped here: the model builds and the rules run on it,
        // but the table can't be created. When this starts failing, the provider has gained
        // JSON columns: turn SupportsJsonColumns on in MySqlFixture.
        await using IntegrationDbContext context = Database.CreateContext(Models.JsonDocuments);

        IReadOnlyList<ModelRuleViolation> violations =
            ModelRuleVerifier.Validate(context, rules => rules.EnumsStoredAsStrings());

        Assert.Equal(2, violations.Count);
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Database.EnsureCreatedAsync(CancellationToken));
        Assert.Contains("JSON columns require", exception.Message, StringComparison.Ordinal);
    }
}
