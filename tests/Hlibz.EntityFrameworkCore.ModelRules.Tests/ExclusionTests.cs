namespace Hlibz.EntityFrameworkCore.ModelRules.Tests;

public sealed class ExclusionTests
{
    [Fact]
    public void Property_WithComplexMemberPath_ExcludesThatMember()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Blog,
            rules => rules.DecimalsHavePrecision(except => except
                .Property<Blog>(x => x.Rating)
                .Property<Blog>(x => x.Address.Latitude)));

        Assert.Empty(violations);
    }

    [Fact]
    public void Entity_WithType_ExcludesEntityAndItsMembers()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Blog,
            rules => rules.StringsHaveMaxLength(except => except.Entity<Blog>()));

        ModelRuleViolation violation = Assert.Single(violations);
        Assert.Equal("Post.Title", violation.Target);
    }

    [Fact]
    public void Entity_WithName_ExcludesMatchingEntity()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Blog,
            rules => rules.StringsHaveMaxLength(except => except.Entity("Blog")));

        Assert.Equal("Post.Title", Assert.Single(violations).Target);
    }

    [Fact]
    public void RuleExclusion_WithOtherRulesRegistered_AppliesOnlyToItsOwnRule()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Blog,
            rules => rules
                .StringsHaveMaxLength(except => except.Entity<Post>())
                .NoShadowProperties());

        Assert.Contains(violations, violation => violation.RuleId == "MR001" && violation.EntityClrType == typeof(Post));
        Assert.DoesNotContain(violations, violation => violation.RuleId == "MR004" && violation.EntityClrType == typeof(Post));
    }

    [Fact]
    public void Except_WithEntity_ExcludesItFromEveryRule()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Blog,
            rules => rules
                .StringsHaveMaxLength()
                .NoShadowProperties()
                .Except(except => except.Entity<Post>()));

        Assert.DoesNotContain(violations, violation => violation.EntityClrType == typeof(Post));
        Assert.NotEmpty(violations);
    }

    [Fact]
    public void Where_WithPredicate_ExcludesMatchingViolations()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Blog,
            rules => rules.StringsHaveMaxLength(except => except.Where(violation =>
                violation.MemberPath?.StartsWith("Address.", StringComparison.Ordinal) == true)));

        Assert.DoesNotContain(violations, violation => violation.Target == "Blog.Address.City");
        Assert.Contains(violations, violation => violation.Target == "Blog.Name");
    }

    [Fact]
    public void Property_WithNonMemberExpression_ThrowsArgumentException()
    {
        ModelRuleExclusions exclusions = new ModelRulesBuilderAccessor().Exclusions;

        Assert.Throws<ArgumentException>(() => exclusions.Property<Blog>(x => x.Name.Length + 1));
    }

    /// <summary>
    /// Captures the exclusions instance the builder hands to a rule's callback.
    /// </summary>
    private sealed class ModelRulesBuilderAccessor
    {
        public ModelRulesBuilderAccessor()
        {
            ModelRuleExclusions? captured = null;
            TestDbContext.Validate(
                Models.Blog,
                rules => rules.NoShadowProperties(except => captured = except));
            Exclusions = captured!;
        }

        public ModelRuleExclusions Exclusions { get; }
    }
}
