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

        Assert.NotEmpty(npgsql);
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
    }
}
