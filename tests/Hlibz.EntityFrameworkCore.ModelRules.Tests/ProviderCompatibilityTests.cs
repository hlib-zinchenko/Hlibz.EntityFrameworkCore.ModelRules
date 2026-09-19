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
        Assert.Empty(Violations(CleanModel, provider));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void UseModelRules_WithProblemModel_ThrowsWhenModelIsBuilt(TestProvider provider)
    {
        using TestDbContext context = new(
            ProblemModel,
            conventions => conventions.UseModelRules(AllRules),
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

    /// <summary>
    /// Every rule except MaxIdentifierLength, whose limit is provider-specific by design.
    /// </summary>
    private static void AllRules(ModelRulesBuilder rules) =>
        rules
            .NoShadowProperties()
            .NamesFollow(NamingStyle.SnakeCase)
            .DecimalsHavePrecision()
            .StringsHaveMaxLength()
            .NullabilityMatchesClr()
            .EnumsStoredAsStrings()
            .SingleSchema()
            .NoCascadeDeleteAcrossAggregates<IAggregateRoot>();

    private static string[] Violations(Action<ModelBuilder> model, TestProvider provider) =>
    [
        .. TestDbContext.Validate(model, AllRules, provider: provider)
            .Select(violation => violation.ToString()),
    ];

    /// <summary>
    /// Breaks every rule in <see cref="AllRules"/> at least once.
    /// </summary>
    private static void ProblemModel(ModelBuilder model)
    {
        Models.Blog(model);
        Models.Countries(model);
        model.Entity<Blog>().Property(x => x.Subtitle).IsRequired();
        model.Entity<State>().ToTable("states", "geo");
    }

    /// <summary>
    /// Passes every rule in <see cref="AllRules"/>: explicit snake_case names everywhere,
    /// precision and max lengths set, the enum stored as a string, and no cascade between roots.
    /// </summary>
    private static void CleanModel(ModelBuilder model)
    {
        model.Entity<Blog>(blog =>
        {
            blog.ToTable("blogs");
            blog.HasKey(x => x.Id).HasName("pk_blogs");
            blog.Property(x => x.Id).HasColumnName("id");
            blog.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
            blog.Property(x => x.Subtitle).HasColumnName("subtitle").HasMaxLength(100);
            blog.Property(x => x.Rating).HasColumnName("rating").HasPrecision(5, 2);
            blog.Property(x => x.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(20);
            blog.ComplexProperty(x => x.Address, address =>
            {
                address.Property(x => x.City).HasColumnName("address_city").HasMaxLength(100);
                address.Property(x => x.Latitude)
                    .HasColumnName("address_latitude")
                    .HasPrecision(9, 6);
            });
            blog.Ignore(x => x.Posts);
        });

        model.Entity<Currency>(currency =>
        {
            currency.ToTable("currencies");
            currency.HasKey(x => x.Id).HasName("pk_currencies");
            currency.Property(x => x.Id).HasColumnName("id");
        });

        model.Entity<Country>(country =>
        {
            country.ToTable("countries");
            country.HasKey(x => x.Id).HasName("pk_countries");
            country.Property(x => x.Id).HasColumnName("id");
            country.Property(x => x.CurrencyId).HasColumnName("currency_id");
            country.Ignore(x => x.States);
            country.HasOne(x => x.Currency)
                .WithMany(x => x.Countries)
                .HasForeignKey(x => x.CurrencyId)
                .HasConstraintName("fk_countries_currencies_currency_id")
                .OnDelete(DeleteBehavior.Restrict);
            country.HasIndex(x => x.CurrencyId).HasDatabaseName("ix_countries_currency_id");
        });
    }
}
