using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Tests;

public sealed class EntryPointTests
{
    [Fact]
    public void UseModelRules_WithViolations_ThrowsWhenModelIsBuilt()
    {
        using TestDbContext context = new(
            Models.Blog,
            conventions => conventions.UseModelRules(rules => rules
                .NoShadowProperties()
                .DecimalsHavePrecision()));

        ModelRuleViolationException exception =
            Assert.Throws<ModelRuleViolationException>(() => context.Model);

        Assert.Equal(3, exception.Violations.Count);
        Assert.Equal(
            """
            The EF Core model has 3 model rule violations:
              - MR001 NoShadowProperties: Post.BlogId: shadow foreign key created by convention for the relationship to Blog. Add a 'BlogId' property to the entity, or configure the relationship with HasForeignKey(...) pointing at an existing one.
            """,
            string.Join(Environment.NewLine, exception.Message.Split(Environment.NewLine).Take(2)));
    }

    [Fact]
    public void UseModelRules_AtModelBuild_ReportsSameViolationsAsDesignTimeValidation()
    {
        using TestDbContext context = new(
            Models.Blog,
            conventions => conventions.UseModelRules(rules => rules
                .NamesFollow(NamingStyle.SnakeCase)
                .StringsHaveMaxLength()
                .DecimalsHavePrecision()));

        ModelRuleViolationException exception =
            Assert.Throws<ModelRuleViolationException>(() => context.Model);

        IReadOnlyList<ModelRuleViolation> designTime = TestDbContext.Validate(
            Models.Blog,
            rules => rules
                .NamesFollow(NamingStyle.SnakeCase)
                .StringsHaveMaxLength()
                .DecimalsHavePrecision());

        Assert.Equal(
            designTime.Select(violation => violation.ToString()),
            exception.Violations.Select(violation => violation.ToString()));
    }

    [Fact]
    public void Verify_WithPassingRegisteredRules_DoesNotThrow()
    {
        using TestDbContext context = new(
            Models.Blog,
            conventions => conventions.UseModelRules(rules => rules
                .NoShadowProperties(except => except.Entity<Post>())));

        ModelRules.Verify(context);
        Assert.NotNull(context.Model);
    }

    [Fact]
    public void Verify_WithFailingRegisteredRules_Throws()
    {
        using TestDbContext context = new(
            Models.Blog,
            conventions => conventions.UseModelRules(rules => rules.NoShadowProperties()));

        Assert.Throws<ModelRuleViolationException>(() => ModelRules.Verify(context));
    }

    [Fact]
    public void Verify_WithoutRegisteredRules_ThrowsInsteadOfPassingSilently()
    {
        using TestDbContext context = new(Models.Blog);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => ModelRules.Verify(context));
        Assert.Contains("has no model rules registered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithRulesPassedIn_ChecksDesignTimeModel()
    {
        using TestDbContext context = new(Models.Blog);

        ModelRuleViolationException exception = Assert.Throws<ModelRuleViolationException>(
            () => ModelRules.Verify(context, rules => rules.EnumsStoredAsStrings()));

        Assert.Equal("Blog.Status", Assert.Single(exception.Violations).Target);
    }

    [Fact]
    public void Verify_WithPrebuiltRuntimeModel_StillChecksDesignTimeModel()
    {
        IModel runtimeModel;
        using (TestDbContext source = new(Models.Blog))
        {
            runtimeModel = source.Model;
        }

        using TestDbContext context = new(
            Models.Blog,
            conventions => conventions.UseModelRules(rules => rules.NoShadowProperties()),
            runtimeModel);

        // At runtime the prebuilt model is used as-is, so the rules never run...
        Assert.Same(runtimeModel, context.Model);

        // ...but Verify builds the design-time model, which does run them.
        Assert.Throws<ModelRuleViolationException>(() => ModelRules.Verify(context));
    }

    [Fact]
    public void Add_WithCustomRule_RunsAlongsideBuiltInRules()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Blog,
            rules => rules.Add(new EntityNamesEndWithSRule()));

        Assert.Equal(
            ["X001 EntityNamesEndWithS: Blog: must end with 's'.", "X001 EntityNamesEndWithS: Post: must end with 's'."],
            violations.Select(violation => violation.ToString()).Order(StringComparer.Ordinal));
    }

    private sealed class EntityNamesEndWithSRule : IModelRule
    {
        public string Id => "X001";

        public string Name => "EntityNamesEndWithS";

        public IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model) =>
            model.GetEntityTypes()
                .Where(entityType => !entityType.ClrType.Name.EndsWith('s'))
                .Select(entityType => new ModelRuleViolation(this, entityType, null, "must end with 's'."));
    }
}
