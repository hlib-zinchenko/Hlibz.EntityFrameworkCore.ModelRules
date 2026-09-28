namespace Hlibz.EntityFrameworkCore.ModelRules.Tests;

/// <summary>
/// MR012 and MR013, the rules about aggregate roots besides MR008's cascade deletes.
/// </summary>
public sealed class AggregateRulesTests
{
    [Fact]
    public void NoNavigationsAcrossAggregates_WithNavigationsBetweenRoots_ReportsBothDirections()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Countries,
            rules => rules.NoNavigationsAcrossAggregates<IAggregateRoot>());

        // Country.States leads to a child entity, not another root, so it's fine.
        Assert.Equal(
            ["Country.Currency", "Currency.Countries"],
            violations.Select(violation => violation.Target).Order(StringComparer.Ordinal));
        Assert.All(violations, violation => Assert.Equal("MR012", violation.RuleId));
        Assert.Contains(
            violations,
            violation => violation.Message.StartsWith(
                "navigation to Currency, a separate aggregate root.",
                StringComparison.Ordinal));
    }

    [Fact]
    public void NoNavigationsAcrossAggregates_WithManyToManyBetweenRoots_ReportsSkipNavigations()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<Author>().HasMany(x => x.Books).WithMany(x => x.Authors),
            rules => rules.NoNavigationsAcrossAggregates<IAggregateRoot>());

        Assert.Equal(
            ["Author.Books", "Book.Authors"],
            violations.Select(violation => violation.Target).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void NoNavigationsAcrossAggregates_WithKeyOnlyReference_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                model.Entity<Currency>().Ignore(x => x.Countries);
                model.Entity<Country>(country =>
                {
                    country.Ignore(x => x.Currency);
                    country.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId);
                });
            },
            rules => rules.NoNavigationsAcrossAggregates<IAggregateRoot>());

        Assert.Empty(violations);
    }

    [Fact]
    public void AggregateRootsHaveConcurrencyToken_WithoutToken_ReportsRootsOnly()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Countries,
            rules => rules.AggregateRootsHaveConcurrencyToken<IAggregateRoot>());

        Assert.Equal(
            ["Country", "Currency"],
            violations.Select(violation => violation.Target).Order(StringComparer.Ordinal));
        Assert.All(violations, violation => Assert.Equal("MR013", violation.RuleId));
    }

    [Fact]
    public void AggregateRootsHaveConcurrencyToken_WithRowVersionOrToken_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                Models.Countries(model);
                model.Entity<Currency>().Property<byte[]>("Version").IsRowVersion();
                model.Entity<Country>().Property(x => x.CurrencyId).IsConcurrencyToken();
            },
            rules => rules.AggregateRootsHaveConcurrencyToken<IAggregateRoot>());

        Assert.Empty(violations);
    }

    [Fact]
    public void AggregateRootsHaveConcurrencyToken_WithHierarchyOfRoots_ReportsTopmostRootOnce()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Accounts,
            rules => rules.AggregateRootsHaveConcurrencyToken<IAggregateRoot>(
                except => except.Entity<Currency>()));

        Assert.Equal("Account", Assert.Single(violations).Target);
    }

    [Fact]
    public void AggregateRootsHaveConcurrencyToken_WithTokenOnBaseType_CoversDerivedTypes()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                Models.Accounts(model);
                model.Entity<Account>().Property<byte[]>("Version").IsRowVersion();
            },
            rules => rules.AggregateRootsHaveConcurrencyToken<IAggregateRoot>(
                except => except.Entity<Currency>()));

        Assert.Empty(violations);
    }
}
