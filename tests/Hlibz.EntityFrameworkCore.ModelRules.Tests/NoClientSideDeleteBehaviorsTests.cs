using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

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
            "deleting Blog rows sets the foreign key to null only on Post rows EF Core is tracking; "
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

    [Theory]
    [InlineData(DeleteBehavior.SetNull)]
    [InlineData(DeleteBehavior.Restrict)]
    public void ConfigureClientSetNullAs_WithDefaultOptionalRelationship_ReplacesBeforeRulesRun(
        DeleteBehavior behavior)
    {
        // Rules registered first: the replacement still runs before them.
        using TestDbContext context = new(
            Models.Blog,
            conventions => conventions
                .UseModelRules(rules => rules.NoClientSideDeleteBehaviors())
                .ConfigureClientSetNullAs(behavior));

        ModelRuleVerifier.Verify(context);

        IForeignKey foreignKey = Assert.Single(
            context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Post))!
                .GetForeignKeys());
        Assert.Equal(behavior, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void ConfigureClientSetNullAs_WithExplicitClientSetNull_KeepsItAndReportsIt()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<Blog>(blog =>
            {
                blog.ComplexProperty(x => x.Address);
                blog.HasMany(x => x.Posts).WithOne().OnDelete(DeleteBehavior.ClientSetNull);
            }),
            rules => rules.NoClientSideDeleteBehaviors(),
            conventions => conventions.ConfigureClientSetNullAs(DeleteBehavior.SetNull));

        Assert.Equal("Post.BlogId", Assert.Single(violations).Target);
    }

    [Fact]
    public void ConfigureClientSetNullAs_WithRequiredRelationship_KeepsCascade()
    {
        using TestDbContext context = new(
            Models.Countries,
            conventions => conventions.ConfigureClientSetNullAs(DeleteBehavior.Restrict));

        IReadOnlyEntityType state =
            context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(State))!;
        Assert.Equal(DeleteBehavior.Cascade, Assert.Single(state.GetForeignKeys()).DeleteBehavior);
    }

    [Fact]
    public void ConfigureClientSetNullAs_WithoutRules_DoesNotCountAsRegisteredRules()
    {
        using TestDbContext context = new(
            Models.Blog,
            conventions => conventions.ConfigureClientSetNullAs(DeleteBehavior.SetNull));

        Assert.Throws<InvalidOperationException>(() => ModelRuleVerifier.Verify(context));
    }

    [Theory]
    [InlineData(DeleteBehavior.ClientSetNull)]
    [InlineData(DeleteBehavior.ClientCascade)]
    [InlineData((DeleteBehavior)42)]
    public void ConfigureClientSetNullAs_WithClientSideBehavior_Throws(DeleteBehavior behavior)
    {
        using TestDbContext context = new(
            Models.Blog,
            conventions => conventions.ConfigureClientSetNullAs(behavior));

        Assert.Throws<ArgumentOutOfRangeException>(() => context.Model);
    }
}
