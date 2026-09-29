using System.Text.RegularExpressions;

using Hlibz.EntityFrameworkCore.ModelRules.IntegrationTests.Databases;

namespace Hlibz.EntityFrameworkCore.ModelRules.IntegrationTests;

/// <summary>
/// Checks, against a real database, that what the rules see in the model is what the database
/// actually creates. Each provider runs these through its own derived class.
/// </summary>
public abstract class DatabaseSchemaTests<TFixture>(TFixture database)
    where TFixture : DatabaseFixture
{
    private static readonly Regex SnakeCase = new("^[a-z][a-z0-9]*(_[a-z0-9]+)*$");

    private static readonly Regex QuotedName = new("'([^']+)'");

    protected TFixture Database { get; } = database;

    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AllRules_WithPassingModel_CreateOnlySnakeCaseIdentifiers()
    {
        await using IntegrationDbContext context =
            Database.CreateContext(Models.Clean, RuleSets.AllProviderNeutral);

        // Building the model runs the registered rules, so this also proves they pass.
        await context.Database.EnsureCreatedAsync(CancellationToken);

        IReadOnlyList<(string Kind, string Name)> identifiers =
            await Database.GetIdentifiersAsync(context);

        Assert.Contains(("table", "blogs"), identifiers);
        Assert.Contains(("column", "address_city"), identifiers);
        Assert.Contains(identifiers, identifier => identifier.Name == "fk_countries_currencies_currency_id");
        Assert.Contains(identifiers, identifier => identifier.Name == "ix_countries_currency_id");
        Assert.Contains(identifiers, identifier => identifier.Name == "ck_blogs_rating");
        Assert.Empty(NotSnakeCase(identifiers));
    }

    [Fact]
    public async Task NamesFollow_WithViolatingColumnName_ReportsTheNameTheDatabaseCreates()
    {
        await using IntegrationDbContext context = Database.CreateContext(model =>
        {
            Models.Clean(model);
            model.Entity<Blog>().Property(x => x.Name).HasColumnName("BlogName");
        });

        IReadOnlyList<ModelRuleViolation> violations =
            ModelRuleVerifier.Validate(context, rules => rules.NamesFollow(NamingStyle.SnakeCase));
        await context.Database.EnsureCreatedAsync(CancellationToken);

        Assert.Equal(
            "column name 'BlogName' is not snake_case.",
            Assert.Single(violations).Message);
        Assert.Equal(["BlogName"], NotSnakeCase(await Database.GetIdentifiersAsync(context)));
    }

    [Fact]
    public async Task NamesFollow_WithTpcHierarchy_ReportsEachTablesNamesTheDatabaseCreates()
    {
        await using IntegrationDbContext context = Database.CreateContext(Models.Accounts);

        IReadOnlyList<ModelRuleViolation> violations = ModelRuleVerifier.Validate(
            context,
            rules => rules.NamesFollow(
                NamingStyle.SnakeCase,
                NamingScope.ForeignKeys | NamingScope.Indexes));
        await context.Database.EnsureCreatedAsync(CancellationToken);

        string[] reported = ReportedNames(violations);
        Assert.Equal(
            [
                "FK_CheckingAccount_Currency_CurrencyId",
                "FK_SavingsAccount_Currency_CurrencyId",
                "IX_CheckingAccount_CurrencyId",
                "IX_SavingsAccount_CurrencyId",
            ],
            reported);

        IReadOnlyList<(string Kind, string Name)> identifiers =
            await Database.GetIdentifiersAsync(context);
        Assert.All(
            reported,
            name => Assert.Contains(identifiers, identifier => identifier.Name == name));
    }

    /// <summary>
    /// The identifier each violation names (the first quoted name in its message), sorted.
    /// </summary>
    protected static string[] ReportedNames(IEnumerable<ModelRuleViolation> violations) =>
    [
        .. violations
            .Select(violation => QuotedName.Match(violation.Message).Groups[1].Value)
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>
    /// The distinct created names that are not snake_case, ignoring names the server assigns
    /// itself.
    /// </summary>
    private string[] NotSnakeCase(IReadOnlyList<(string Kind, string Name)> identifiers) =>
    [
        .. identifiers
            .Select(identifier => identifier.Name)
            .Where(name => !Database.ServerAssignedNames.Contains(name) && !SnakeCase.IsMatch(name))
            .Distinct()
            .Order(StringComparer.Ordinal),
    ];
}
