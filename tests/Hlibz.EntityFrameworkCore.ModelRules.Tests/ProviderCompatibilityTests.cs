namespace Hlibz.EntityFrameworkCore.ModelRules.Tests;

/// <summary>
/// Every other test builds its models with Npgsql. These build the same models with every
/// provider, to show the rules only depend on EF Core's provider-neutral metadata.
/// </summary>
public sealed class ProviderCompatibilityTests
{
    public static TheoryData<TestProvider> Providers => new(Enum.GetValues<TestProvider>());

    public static TheoryData<TestProvider> JsonProviders =>
        new(TestProvider.Npgsql, TestProvider.SqlServer, TestProvider.Sqlite);

    [Theory]
    [MemberData(nameof(Providers))]
    public void AllRules_WithProblemModel_ReportSameViolationsAsNpgsql(TestProvider provider)
    {
        string[] npgsql = Violations(ProblemModel, TestProvider.Npgsql);
        string[] actual = Violations(ProblemModel, provider);

        Assert.Equal(
            ["MR001", "MR002", "MR003", "MR004", "MR005", "MR006", "MR007", "MR008", "MR010", "MR011", "MR012", "MR014"],
            npgsql.Select(violation => violation.Split(' ')[0]).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(npgsql, actual);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void AllRules_WithCleanModel_ReportNothing(TestProvider provider)
    {
        Assert.Empty(Violations(Models.Clean, provider));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void UseModelRules_WithProblemModel_ThrowsWhenModelIsBuilt(TestProvider provider)
    {
        using TestDbContext context = new(
            ProblemModel,
            conventions => conventions.UseModelRules(RuleSets.AllProviderNeutral),
            provider: provider);

        ModelRuleViolationException exception =
            Assert.Throws<ModelRuleViolationException>(() => context.Model);
        Assert.Equal(
            Violations(ProblemModel, provider),
            exception.Violations.Select(violation => violation.ToString()));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void NamesFollow_WithTpcAndCheckConstraint_ReportSameViolationsAsNpgsql(
        TestProvider provider)
    {
        // Sequences are left out: only some providers generate TPC keys with one.
        static void Rules(ModelRulesBuilder rules) =>
            rules.NamesFollow(NamingStyle.SnakeCase, NamingScope.All & ~NamingScope.Sequences);

        static void Model(ModelBuilder model)
        {
            Models.Accounts(model);
            model.Entity<State>().ToTable(
                table => table.HasCheckConstraint("CK_State_CountryId", "country_id > 0"));
        }

        string[] npgsql =
        [
            .. TestDbContext.Validate(Model, Rules)
                .Select(violation => violation.ToString()),
        ];
        string[] actual =
        [
            .. TestDbContext.Validate(Model, Rules, provider: provider)
                .Select(violation => violation.ToString()),
        ];

        Assert.Contains(npgsql, violation => violation.Contains("FK_SavingsAccount", StringComparison.Ordinal));
        Assert.Contains(npgsql, violation => violation.Contains("CK_State_CountryId", StringComparison.Ordinal));
        Assert.Equal(npgsql, actual);
    }

    [Theory]
    [MemberData(nameof(JsonProviders))]
    public void NamesFollow_WithJsonOwnedType_ChecksOnlyContainerColumn(TestProvider provider)
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<Invoice>(invoice =>
            {
                invoice.ToTable("invoices");
                invoice.HasKey(x => x.Id).HasName("pk_invoices");
                invoice.Property(x => x.Id).HasColumnName("id");
                invoice.Property(x => x.Total)
                    .HasConversion(x => x.Amount, x => new Money(x))
                    .HasColumnName("total");
                invoice.OwnsOne(x => x.Contact, contact => contact.ToJson("ContactInfo"));
            }),
            rules => rules.NamesFollow(NamingStyle.SnakeCase),
            provider: provider);

        Assert.Equal(
            "column name 'ContactInfo' is not snake_case.",
            Assert.Single(violations).Message);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void EnumsStoredAsStrings_WithEnumCollection_ReportSameViolationsAsNpgsql(
        TestProvider provider)
    {
        static void Rules(ModelRulesBuilder rules) => rules.EnumsStoredAsStrings();

        string[] npgsql =
        [
            .. TestDbContext.Validate(model => model.Entity<Article>(), Rules)
                .Select(violation => violation.ToString()),
        ];
        string[] actual =
        [
            .. TestDbContext.Validate(model => model.Entity<Article>(), Rules, provider: provider)
                .Select(violation => violation.ToString()),
        ];

        Assert.Equal(2, npgsql.Length);
        Assert.Equal(npgsql, actual);
    }

    [Theory]
    [MemberData(nameof(JsonProviders))]
    public void EnumsStoredAsStrings_WithEnumsInJsonOwnedType_ReportSameViolationsAsNpgsql(
        TestProvider provider)
    {
        static void Rules(ModelRulesBuilder rules) => rules.EnumsStoredAsStrings();

        string[] npgsql =
        [
            .. TestDbContext.Validate(Models.JsonDocuments, Rules)
                .Select(violation => violation.ToString()),
        ];
        string[] actual =
        [
            .. TestDbContext.Validate(Models.JsonDocuments, Rules, provider: provider)
                .Select(violation => violation.ToString()),
        ];

        Assert.Equal(2, npgsql.Length);
        Assert.Equal(npgsql, actual);
    }

    [Fact]
    public void NoShadowProperties_WithSqlServerTemporalTable_AllowsPeriodColumns()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<State>().ToTable("states", table => table.IsTemporal()),
            rules => rules.NoShadowProperties(),
            provider: TestProvider.SqlServer);

        Assert.Empty(violations);
    }

    [Fact]
    public void NoShadowProperties_WithCustomTemporalPeriodNames_AllowsPeriodColumns()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<State>().ToTable("states", table => table.IsTemporal(temporal =>
            {
                temporal.HasPeriodStart("ValidFrom");
                temporal.HasPeriodEnd("ValidTo");
            })),
            rules => rules.NoShadowProperties(),
            provider: TestProvider.SqlServer);

        Assert.Empty(violations);
    }

    private static string[] Violations(Action<ModelBuilder> model, TestProvider provider) =>
    [
        .. TestDbContext.Validate(model, RuleSets.AllProviderNeutral, provider: provider)
            .Select(violation => violation.ToString()),
    ];

    /// <summary>
    /// Breaks every rule in <see cref="RuleSets.AllProviderNeutral"/> at least once.
    /// </summary>
    private static void ProblemModel(ModelBuilder model)
    {
        Models.Blog(model);
        Models.Countries(model);
        model.Entity<Blog>().Property(x => x.Subtitle).IsRequired();
        model.Entity<State>().ToTable("states", "geo");
        model.Entity<State>().HasIndex(x => x.Id);
        model.Entity<Comment>();
    }
}
