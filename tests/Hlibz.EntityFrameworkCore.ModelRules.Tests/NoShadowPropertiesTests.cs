namespace Hlibz.EntityFrameworkCore.ModelRules.Tests;

public sealed class NoShadowPropertiesTests
{
    [Fact]
    public void NoShadowProperties_WithConventionForeignKey_ReportsShadowForeignKey()
    {
        IReadOnlyList<ModelRuleViolation> violations =
            TestDbContext.Validate(Models.Blog, rules => rules.NoShadowProperties());

        ModelRuleViolation violation = Assert.Single(violations);
        Assert.Equal("MR001", violation.RuleId);
        Assert.Equal(typeof(Post), violation.EntityClrType);
        Assert.Equal("BlogId", violation.MemberPath);
        Assert.Contains("shadow foreign key", violation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoShadowProperties_WithTphDiscriminator_AllowsIt()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                model.Entity<Animal>();
                model.Entity<Dog>();
                model.Entity<Cat>();
            },
            rules => rules.NoShadowProperties());

        Assert.Empty(violations);
    }

    [Fact]
    public void NoShadowProperties_WithOwnedType_AllowsSyntheticKey()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<Invoice>(invoice =>
            {
                invoice.Property(x => x.Total).HasConversion(x => x.Amount, x => new Money(x));
                invoice.OwnsOne(x => x.Contact);
            }),
            rules => rules.NoShadowProperties());

        Assert.Empty(violations);
    }

    [Fact]
    public void NoShadowProperties_WithShadowPropertyExcludedByName_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Blog,
            rules => rules.NoShadowProperties(except => except.Property<Post>("BlogId")));

        Assert.Empty(violations);
    }
}
