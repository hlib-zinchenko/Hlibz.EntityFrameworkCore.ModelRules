using System.Text.Json;
using System.Text.RegularExpressions;

using Hlibz.EntityFrameworkCore.ModelRules.IntegrationTests.Databases;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

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

    [Fact]
    public async Task NamesFollow_WithEntitySplitting_ReportsSecondTablesNamesTheDatabaseCreates()
    {
        await using IntegrationDbContext context = Database.CreateContext(Models.Profiles);

        IReadOnlyList<ModelRuleViolation> violations = ModelRuleVerifier.Validate(
            context,
            rules => rules.NamesFollow(
                NamingStyle.SnakeCase,
                NamingScope.Tables | NamingScope.Columns | NamingScope.ForeignKeys));
        await context.Database.EnsureCreatedAsync(CancellationToken);

        // Id is a column of both tables.
        string[] reported = ReportedNames(violations);
        Assert.Equal(
            [
                "Bio",
                "FK_ProfileDetails_profiles_Id",
                "Id",
                "Id",
                "Name",
                "ProfileDetails",
                "Website",
            ],
            reported);

        IReadOnlyList<(string Kind, string Name)> identifiers =
            await Database.GetIdentifiersAsync(context);
        Assert.All(
            reported,
            name => Assert.Contains(identifiers, identifier => identifier.Name == name));
    }

    [Fact]
    public async Task EnumsStoredAsStrings_WithEnumsInJsonOwnedType_ReportsStoredNumbers()
    {
        SkipUnlessJsonColumns();

        await using IntegrationDbContext context = Database.CreateContext(Models.JsonDocuments);

        IReadOnlyList<ModelRuleViolation> violations =
            ModelRuleVerifier.Validate(context, rules => rules.EnumsStoredAsStrings());

        Assert.Equal(
            ["DocumentMeta.Status", "DocumentMeta.Statuses"],
            violations.Select(violation => violation.Target).Order(StringComparer.Ordinal));
        Assert.Equal(
            """{"Status":1,"Statuses":[1,0]}""",
            await SaveAndReadEnumsAsync(context));
    }

    [Fact]
    public async Task EnumsStoredAsStrings_WithConvertedEnumsInJsonOwnedType_StoresNames()
    {
        SkipUnlessJsonColumns();

        await using IntegrationDbContext context = Database.CreateContext(
            model => model.Entity<Document>().OwnsOne(x => x.Meta, meta =>
            {
                meta.ToJson();
                meta.Property(x => x.Status).HasConversion<string>();
                meta.PrimitiveCollection(x => x.Statuses).ElementType().HasConversion<string>();
            }));

        IReadOnlyList<ModelRuleViolation> violations =
            ModelRuleVerifier.Validate(context, rules => rules.EnumsStoredAsStrings());

        Assert.Empty(violations);
        Assert.Equal(
            """{"Status":"Published","Statuses":["Published","Draft"]}""",
            await SaveAndReadEnumsAsync(context));
    }

    [Fact]
    public async Task EnumsStoredAsStrings_WithEnumsInJsonComplexType_ReportsStoredNumbers()
    {
        SkipUnlessJsonColumns();

        await using IntegrationDbContext context = Database.CreateContext(
            model => model.Entity<Document>().ComplexProperty(x => x.Meta, meta => meta.ToJson()));

        IReadOnlyList<ModelRuleViolation> violations =
            ModelRuleVerifier.Validate(context, rules => rules.EnumsStoredAsStrings());

        Assert.Equal(
            ["Document.Meta.Status", "Document.Meta.Statuses"],
            violations.Select(violation => violation.Target).Order(StringComparer.Ordinal));
        Assert.Equal(
            """{"Status":1,"Statuses":[1,0]}""",
            await SaveAndReadEnumsAsync(context));
    }

    protected void SkipUnlessJsonColumns() =>
        Assert.SkipUnless(
            Database.SupportsJsonColumns,
            "The provider doesn't support JSON columns.");

    /// <summary>
    /// Creates the database, saves a document whose metadata holds one enum and a collection of
    /// enums, and reads back how the database stored them: the <c>Status</c> and
    /// <c>Statuses</c> members of the raw JSON column, as compact JSON. Parsed rather than
    /// compared as text, since PostgreSQL's <c>jsonb</c> reorders members and adds whitespace.
    /// </summary>
    protected static async Task<string> SaveAndReadEnumsAsync(DbContext context)
    {
        await context.Database.EnsureCreatedAsync(CancellationToken);
        context.Add(new Document
        {
            Meta = new DocumentMeta
            {
                Title = "Release notes",
                Status = BlogStatus.Published,
                Statuses = [BlogStatus.Published, BlogStatus.Draft],
            },
        });
        await context.SaveChangesAsync(CancellationToken);

        ISqlGenerationHelper sql = context.GetService<ISqlGenerationHelper>();
        object?[] row = Assert.Single(await DatabaseFixture.QueryAsync(
            context,
            $"select {sql.DelimitIdentifier("Meta")} from {sql.DelimitIdentifier("Document")}"));

        using JsonDocument json = JsonDocument.Parse(Assert.IsType<string>(row[0]));
        JsonElement meta = json.RootElement;
        return JsonSerializer.Serialize(new
        {
            Status = meta.GetProperty("Status"),
            Statuses = meta.GetProperty("Statuses"),
        });
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
