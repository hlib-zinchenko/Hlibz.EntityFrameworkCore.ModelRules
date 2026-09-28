namespace Hlibz.EntityFrameworkCore.ModelRules.Tests;

public sealed class NoRedundantIndexesTests
{
    [Fact]
    public void NoRedundantIndexes_WithPrefixOfCompositeIndex_ReportsShorterIndex()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<State>(state =>
            {
                state.HasIndex(x => x.CountryId).HasDatabaseName("ix_states_country");
                state.HasIndex(x => new { x.CountryId, x.Id }).HasDatabaseName("ix_states_country_id");
            }),
            rules => rules.NoRedundantIndexes());

        ModelRuleViolation violation = Assert.Single(violations);
        Assert.Equal("MR014", violation.RuleId);
        Assert.Equal("State.CountryId", violation.Target);
        Assert.Equal(
            "index 'ix_states_country' (CountryId) is a leading prefix of index "
            + "'ix_states_country_id' (CountryId, Id), which serves the same lookups. Remove it.",
            violation.Message);
    }

    [Fact]
    public void NoRedundantIndexes_WithIndexOnPrimaryKey_ReportsIt()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<State>().HasIndex(x => x.Id).HasDatabaseName("ix_states_id"),
            rules => rules.NoRedundantIndexes());

        Assert.StartsWith(
            "index 'ix_states_id' (Id) has the same columns as primary key 'PK_State' (Id)",
            Assert.Single(violations).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NoRedundantIndexes_WithIdenticalIndexes_ReportsOnlyOne()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<State>(state =>
            {
                state.HasIndex(x => x.CountryId, "first").HasDatabaseName("ix_a");
                state.HasIndex(x => x.CountryId, "second").HasDatabaseName("ix_b");
            }),
            rules => rules.NoRedundantIndexes());

        Assert.StartsWith(
            "index 'ix_b' (CountryId) has the same columns as index 'ix_a'",
            Assert.Single(violations).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NoRedundantIndexes_WithUniquePrefix_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<State>(state =>
            {
                state.HasIndex(x => x.CountryId).IsUnique();
                state.HasIndex(x => new { x.CountryId, x.Id });
            }),
            rules => rules.NoRedundantIndexes());

        Assert.Empty(violations);
    }

    [Fact]
    public void NoRedundantIndexes_WithFilterOrDifferentSortOrder_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<State>(state =>
            {
                state.HasIndex(x => x.CountryId, "filtered").HasFilter("country_id > 0");
                state.HasIndex(x => x.CountryId, "descending").IsDescending();
                state.HasIndex(x => new { x.CountryId, x.Id });
            }),
            rules => rules.NoRedundantIndexes());

        Assert.Empty(violations);
    }

    [Fact]
    public void NoRedundantIndexes_WithCompositeIndexCoveringForeignKey_ReportsNothing()
    {
        // EF Core drops its own foreign key index when another index already starts with it.
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                Models.Countries(model);
                model.Entity<Country>().HasIndex(x => new { x.CurrencyId, x.Id });
            },
            rules => rules.NoRedundantIndexes());

        Assert.Empty(violations);
    }

    [Fact]
    public void NoRedundantIndexes_WithTpcHierarchy_ReportsEachConcreteTable()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                Models.Accounts(model);
                model.Entity<Account>().HasIndex(x => x.Id);
            },
            rules => rules.NoRedundantIndexes());

        Assert.Equal(
            ["IX_CheckingAccount_Id", "IX_SavingsAccount_Id"],
            violations
                .Select(violation => violation.Message.Split('\'')[1])
                .Order(StringComparer.Ordinal));
    }
}
