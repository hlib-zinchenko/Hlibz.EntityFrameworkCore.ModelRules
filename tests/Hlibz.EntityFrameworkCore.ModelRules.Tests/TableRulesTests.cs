namespace Hlibz.EntityFrameworkCore.ModelRules.Tests;

public sealed class TableRulesTests
{
    [Fact]
    public void SingleSchema_WithoutExpectedSchema_ReportsTablesOutsideMajoritySchema()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                model.HasDefaultSchema("blogging");
                Models.Blog(model);
                model.Entity<State>().ToTable("states", "geo");
            },
            rules => rules.SingleSchema());

        ModelRuleViolation violation = Assert.Single(violations);
        Assert.Equal("MR007", violation.RuleId);
        Assert.Equal(typeof(State), violation.EntityClrType);
        Assert.Equal(
            "mapped to schema 'geo', but the model's tables belong in 'blogging'.",
            violation.Message);
    }

    [Fact]
    public void SingleSchema_WithExpectedSchema_ReportsEveryOtherTable()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                Models.Blog(model);
                model.Entity<State>().ToTable("states", "geo");
            },
            rules => rules.SingleSchema("geo"));

        Assert.Equal(
            ["Blog", "Post"],
            violations.Select(violation => violation.Target).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void SingleSchema_WithSequenceAndFunctionInOtherSchemas_ReportsThem()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                model.HasDefaultSchema("billing");
                Models.Blog(model);
                model.HasSequence<long>("invoice_numbers");
                model.HasSequence<long>("order_numbers", "sales");
                model.HasDbFunction(
                        typeof(ReportFunctions).GetMethod(nameof(ReportFunctions.OrderTotal))!)
                    .HasSchema("reporting");
            },
            rules => rules.SingleSchema());

        Assert.Equal(
            [
                "function reporting.OrderTotal: mapped to schema 'reporting', but the model's "
                + "tables belong in 'billing'.",
                "sequence sales.order_numbers: mapped to schema 'sales', but the model's tables "
                + "belong in 'billing'.",
            ],
            violations
                .Select(violation => $"{violation.Target}: {violation.Message}")
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void SingleSchema_WithOnlySequences_UsesTheirMajoritySchema()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                model.HasSequence<long>("first_numbers", "sales");
                model.HasSequence<long>("second_numbers", "sales");
                model.HasSequence<long>("stray_numbers", "misc");
            },
            rules => rules.SingleSchema());

        Assert.Equal("sequence misc.stray_numbers", Assert.Single(violations).Target);
    }

    [Fact]
    public void SingleSchema_WithSameTableNameInAnotherSchema_ReportsIt()
    {
        // Post sorts before State, so State's table is the second "items" the rule sees.
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                model.Entity<Post>().ToTable("items", "a");
                model.Entity<State>().ToTable("items", "b");
            },
            rules => rules.SingleSchema("a"));

        ModelRuleViolation violation = Assert.Single(violations);
        Assert.Equal(typeof(State), violation.EntityClrType);
    }

    [Fact]
    public void NoCascadeDeleteAcrossAggregates_WithCascadeBetweenRoots_ReportsRootToRootOnly()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Countries,
            rules => rules.NoCascadeDeleteAcrossAggregates<IAggregateRoot>());

        ModelRuleViolation violation = Assert.Single(violations);
        Assert.Equal("MR008", violation.RuleId);
        Assert.Equal("Country.Currency", violation.Target);
        Assert.StartsWith(
            "deleting Currency rows cascades to Country",
            violation.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NoCascadeDeleteAcrossAggregates_WithRestrictBetweenRoots_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                Models.Countries(model);
                model.Entity<Country>()
                    .HasOne(x => x.Currency)
                    .WithMany(x => x.Countries)
                    .OnDelete(DeleteBehavior.Restrict);
            },
            rules => rules.NoCascadeDeleteAcrossAggregates(type => type != typeof(State)));

        Assert.Empty(violations);
    }

    [Fact]
    public void MaxIdentifierLength_WithLongExplicitNames_ReportsThem()
    {
        string longTable = new('t', 64);
        string longIndex = "ix_" + new string('i', 70);

        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<State>(state =>
            {
                state.ToTable(longTable);
                state.HasIndex(x => x.CountryId).HasDatabaseName(longIndex);
            }),
            rules => rules.MaxIdentifierLength(63));

        Assert.Equal(2, violations.Count);
        Assert.Contains(violations, violation => violation.Message.StartsWith($"table name '{longTable}' is 64", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Message.StartsWith($"index name '{longIndex}' is 73", StringComparison.Ordinal));
    }

    [Fact]
    public void MaxIdentifierLength_WithSameTableNameInTwoSchemas_ReportsBoth()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                model.Entity<Post>().ToTable("items", "a");
                model.Entity<State>().ToTable("items", "b");
            },
            rules => rules.MaxIdentifierLength(3, NamingScope.Tables));

        Assert.Equal(
            ["Post", "State"],
            violations.Select(violation => violation.Target).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void MaxIdentifierLength_WithLongSequenceName_ReportsIt()
    {
        // Under the provider's own limit: EF Core shortens a longer sequence name itself.
        string longSequence = new('s', 40);

        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                Models.Blog(model);
                model.HasSequence<long>(longSequence);
            },
            rules => rules.MaxIdentifierLength(30, NamingScope.Sequences));

        ModelRuleViolation violation = Assert.Single(violations);
        Assert.Equal($"sequence {longSequence}", violation.Target);
        Assert.StartsWith(
            $"sequence name '{longSequence}' is 40",
            violation.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MaxIdentifierLength_WithEfGeneratedNames_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<State>(state =>
            {
                state.ToTable(new string('t', 63));
                state.HasIndex(x => x.CountryId);
            }),
            rules => rules.MaxIdentifierLength(63));

        Assert.Empty(violations);
    }
}
