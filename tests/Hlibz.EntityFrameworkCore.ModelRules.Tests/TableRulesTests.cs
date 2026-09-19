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
    public void NoCascadeDeleteAcrossAggregates_WithCascadeBetweenRoots_ReportsRootToRootOnly()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Countries,
            rules => rules.NoCascadeDeleteAcrossAggregates<IAggregateRoot>());

        ModelRuleViolation violation = Assert.Single(violations);
        Assert.Equal("MR008", violation.RuleId);
        Assert.Equal("Country.Currency", violation.Target);
        Assert.StartsWith(
            "deleting a Currency cascades to Country",
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
