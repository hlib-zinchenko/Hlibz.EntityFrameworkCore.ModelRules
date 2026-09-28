namespace Hlibz.EntityFrameworkCore.ModelRules.Tests;

public sealed class NoClientSideDeleteBehaviorsTests
{
    [Fact]
    public void NoClientSideDeleteBehaviors_WithOptionalRelationshipDefault_ReportsClientSetNull()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Blog,
            rules => rules.NoClientSideDeleteBehaviors());

        ModelRuleViolation violation = Assert.Single(violations);
        Assert.Equal("MR011", violation.RuleId);
        Assert.Equal("Post.BlogId", violation.Target);
        Assert.Equal(
            "deleting a Blog sets the foreign key to null only on Post rows EF Core is tracking; "
            + "the database constraint does nothing, so the delete fails when any other row "
            + "still refers to it. Configure OnDelete(DeleteBehavior.SetNull), or "
            + "OnDelete(DeleteBehavior.Restrict) to forbid it.",
            violation.Message);
    }

    [Fact]
    public void NoClientSideDeleteBehaviors_WithClientCascade_SuggestsDatabaseCascade()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                Models.Countries(model);
                model.Entity<Country>()
                    .HasMany(x => x.States)
                    .WithOne()
                    .HasForeignKey(x => x.CountryId)
                    .OnDelete(DeleteBehavior.ClientCascade);
            },
            rules => rules.NoClientSideDeleteBehaviors());

        ModelRuleViolation violation = Assert.Single(violations);
        Assert.Equal("State.CountryId", violation.Target);
        Assert.Contains("OnDelete(DeleteBehavior.Cascade)", violation.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DeleteBehavior.SetNull)]
    [InlineData(DeleteBehavior.Restrict)]
    [InlineData(DeleteBehavior.NoAction)]
    [InlineData(DeleteBehavior.ClientNoAction)]
    [InlineData(DeleteBehavior.Cascade)]
    public void NoClientSideDeleteBehaviors_WithDatabaseBehavior_ReportsNothing(DeleteBehavior behavior)
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<Blog>(blog =>
            {
                blog.ComplexProperty(x => x.Address);
                blog.HasMany(x => x.Posts).WithOne().OnDelete(behavior);
            }),
            rules => rules.NoClientSideDeleteBehaviors());

        Assert.Empty(violations);
    }

    [Fact]
    public void NoClientSideDeleteBehaviors_WithTpcBaseType_ReportsForeignKeyOnConcreteTables()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                Models.Accounts(model);
                model.Entity<Account>()
                    .HasOne<Currency>()
                    .WithMany()
                    .HasForeignKey(x => x.CurrencyId)
                    .OnDelete(DeleteBehavior.ClientCascade);
            },
            rules => rules.NoClientSideDeleteBehaviors());

        Assert.Equal("Account.CurrencyId", Assert.Single(violations).Target);
    }

    [Fact]
    public void NoClientSideDeleteBehaviors_WithViewMappedDependent_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                Models.Blog(model);
                model.Entity<Post>().ToView("posts_view");
            },
            rules => rules.NoClientSideDeleteBehaviors());

        Assert.Empty(violations);
    }
}
