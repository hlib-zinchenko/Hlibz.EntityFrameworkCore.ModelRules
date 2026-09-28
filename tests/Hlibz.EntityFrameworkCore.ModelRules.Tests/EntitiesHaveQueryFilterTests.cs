namespace Hlibz.EntityFrameworkCore.ModelRules.Tests;

public sealed class EntitiesHaveQueryFilterTests
{
    [Fact]
    public void EntitiesHaveQueryFilter_WithoutFilter_ReportsMarkedEntityOnly()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                model.Entity<Comment>();
                model.Entity<State>();
            },
            rules => rules.EntitiesHaveQueryFilter<ISoftDeletable>());

        ModelRuleViolation violation = Assert.Single(violations);
        Assert.Equal("MR010", violation.RuleId);
        Assert.Equal("Comment", violation.Target);
        Assert.Equal(
            "implements ISoftDeletable but has no query filter. Configure HasQueryFilter(...).",
            violation.Message);
    }

    [Fact]
    public void EntitiesHaveQueryFilter_WithFilter_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<Comment>().HasQueryFilter(x => !x.IsDeleted),
            rules => rules.EntitiesHaveQueryFilter<ISoftDeletable>());

        Assert.Empty(violations);
    }

    [Fact]
    public void EntitiesHaveQueryFilter_WithMarkedDerivedType_PointsAtRootType()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                model.Entity<Note>();
                model.Entity<ArchivedNote>();
            },
            rules => rules.EntitiesHaveQueryFilter<ISoftDeletable>());

        ModelRuleViolation violation = Assert.Single(violations);
        Assert.Equal("ArchivedNote", violation.Target);
        Assert.StartsWith(
            "implements ISoftDeletable but its root type Note has no query filter",
            violation.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EntitiesHaveQueryFilter_WithFilterOnRootType_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                model.Entity<Note>().HasQueryFilter(x => !(x is ArchivedNote && ((ArchivedNote)x).IsDeleted));
                model.Entity<ArchivedNote>();
            },
            rules => rules.EntitiesHaveQueryFilter<ISoftDeletable>());

        Assert.Empty(violations);
    }

    [Fact]
    public void EntitiesHaveQueryFilter_WithPredicate_UsesGenericReason()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<Comment>(),
            rules => rules.EntitiesHaveQueryFilter(type => type == typeof(Comment)));

        Assert.Equal(
            "needs a query filter but has no query filter. Configure HasQueryFilter(...).",
            Assert.Single(violations).Message);
    }
}
