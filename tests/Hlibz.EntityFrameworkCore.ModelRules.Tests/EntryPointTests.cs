using Microsoft.EntityFrameworkCore.Infrastructure;
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
              - MR001 NoShadowProperties: Post.BlogId: shadow foreign key created by convention for the relationship to Blog. Add a property named 'BlogId' to the entity, or configure the relationship with HasForeignKey(...) pointing at an existing one.
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

        ModelRuleVerifier.Verify(context);
        Assert.NotNull(context.Model);
    }

    [Fact]
    public void Verify_WithFailingRegisteredRules_Throws()
    {
        using TestDbContext context = new(
            Models.Blog,
            conventions => conventions.UseModelRules(rules => rules.NoShadowProperties()));

        Assert.Throws<ModelRuleViolationException>(() => ModelRuleVerifier.Verify(context));
    }

    [Fact]
    public void Verify_WithoutRegisteredRules_ThrowsInsteadOfPassingSilently()
    {
        using TestDbContext context = new(Models.Blog);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => ModelRuleVerifier.Verify(context));
        Assert.Contains("has no model rules registered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithRulesPassedIn_ChecksDesignTimeModel()
    {
        using TestDbContext context = new(Models.Blog);

        ModelRuleViolationException exception = Assert.Throws<ModelRuleViolationException>(
            () => ModelRuleVerifier.Verify(context, rules => rules.EnumsStoredAsStrings()));

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
        Assert.Throws<ModelRuleViolationException>(() => ModelRuleVerifier.Verify(context));
    }

    [Fact]
    public void Verify_WithOptions_CreatesContextAndRunsRegisteredRules()
    {
        Assert.Throws<ModelRuleViolationException>(
            () => ModelRuleVerifier.Verify(OptionsDbContext.CreateOptions()));
    }

    [Fact]
    public void Validate_WithOptions_ChecksRulesPassedIn()
    {
        IReadOnlyList<ModelRuleViolation> violations = ModelRuleVerifier.Validate(
            PassingOptionsDbContext.CreateOptions(),
            rules => rules.EnumsStoredAsStrings());

        Assert.Equal("Blog.Status", Assert.Single(violations).Target);
    }

    [Fact]
    public void Verify_WithUntypedOptions_ThrowsArgumentException()
    {
        DbContextOptions options =
            new DbContextOptionsBuilder().UseNpgsql("Server=localhost").Options;

        ArgumentException exception =
            Assert.Throws<ArgumentException>(() => ModelRuleVerifier.Verify(options));
        Assert.Contains(
            "DbContextOptionsBuilder<TContext>",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithOptionsForContextWithoutOptionsConstructor_ThrowsArgumentException()
    {
        DbContextOptions options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql("Server=localhost")
            .Options;

        ArgumentException exception =
            Assert.Throws<ArgumentException>(() => ModelRuleVerifier.Verify(options));
        Assert.Contains("no public constructor", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyAll_WithFailingContexts_ReportsEveryContextInOneException()
    {
        ModelRuleViolationException exception = Assert.Throws<ModelRuleViolationException>(
            () => ModelRuleVerifier.VerifyAll(
                () => new ServiceDbContext(ServiceDbContext.CreateOptions(), new Clock()),
                () => new OptionsDbContext(OptionsDbContext.CreateOptions()),
                () => new ServiceDbContext(
                    ServiceDbContext.CreateOptions(),
                    new Clock(),
                    rules => rules.NoShadowProperties(except => except.Entity<Post>()))));

        Assert.Equal(2, exception.Violations.Count);
        Assert.Equal(
            """
            2 of 3 EF Core models have model rule violations:
            ServiceDbContext has 1 violation:
              - MR001 NoShadowProperties: Post.BlogId: shadow foreign key created by convention for the relationship to Blog. Add a property named 'BlogId' to the entity, or configure the relationship with HasForeignKey(...) pointing at an existing one.
            OptionsDbContext has 1 violation:
              - MR001 NoShadowProperties: Post.BlogId: shadow foreign key created by convention for the relationship to Blog. Add a property named 'BlogId' to the entity, or configure the relationship with HasForeignKey(...) pointing at an existing one.
            """,
            exception.Message.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void VerifyAll_WithPassingContexts_DoesNotThrow()
    {
        ModelRuleVerifier.VerifyAll(
            () => new ServiceDbContext(
                ServiceDbContext.CreateOptions(),
                new Clock(),
                rules => rules.NoShadowProperties(except => except.Entity<Post>())));
    }

    [Fact]
    public void VerifyAll_WithContextWithoutRegisteredRules_Throws()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ModelRuleVerifier.VerifyAll(
                () => new PassingOptionsDbContext(PassingOptionsDbContext.CreateOptions())));

        Assert.Contains(
            "PassingOptionsDbContext has no model rules registered",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyAll_WithOptions_CreatesEachContext()
    {
        ModelRuleViolationException exception = Assert.Throws<ModelRuleViolationException>(
            () => ModelRuleVerifier.VerifyAll(OptionsDbContext.CreateOptions()));

        Assert.StartsWith(
            "1 of 1 EF Core model has model rule violations:",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyAll_WithNoContexts_ThrowsInsteadOfPassingSilently()
    {
        Assert.Throws<ArgumentException>(
            () => ModelRuleVerifier.VerifyAll(Array.Empty<Func<DbContext>>()));
        Assert.Throws<ArgumentException>(
            () => ModelRuleVerifier.VerifyAll(Array.Empty<DbContextOptions>()));
    }

    [Fact]
    public void VerifyAll_WithFactoryReturningNull_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ModelRuleVerifier.VerifyAll(() => null!));
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

    /// <summary>
    /// A context built only from its options, the way a DI-registered context is.
    /// </summary>
    public sealed class OptionsDbContext(DbContextOptions<OptionsDbContext> options)
        : DbContext(options)
    {
        public static DbContextOptions<OptionsDbContext> CreateOptions() =>
            new DbContextOptionsBuilder<OptionsDbContext>().UseNpgsql("Server=localhost").Options;

        protected override void ConfigureConventions(ModelConfigurationBuilder conventions) =>
            conventions.UseModelRules(rules => rules.NoShadowProperties());

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            Models.Blog(modelBuilder);
    }

    /// <summary>
    /// Takes the non-generic options, which <c>ModelRuleVerifier</c> also accepts.
    /// </summary>
    public sealed class PassingOptionsDbContext(DbContextOptions options) : DbContext(options)
    {
        public static DbContextOptions<PassingOptionsDbContext> CreateOptions() =>
            new DbContextOptionsBuilder<PassingOptionsDbContext>()
                .UseNpgsql("Server=localhost")
                .Options;

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            Models.Blog(modelBuilder);
    }

    public sealed class Clock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Takes a service besides its options, as contexts in a modular monolith often do, so only a
    /// factory can create it. Each instance builds its own model, since the rules vary per test.
    /// </summary>
    public sealed class ServiceDbContext(
        DbContextOptions<ServiceDbContext> options,
        Clock clock,
        Action<ModelRulesBuilder>? rules = null) : DbContext(options)
    {
        public Clock Clock { get; } = clock;

        public static DbContextOptions<ServiceDbContext> CreateOptions() =>
            new DbContextOptionsBuilder<ServiceDbContext>()
                .UseNpgsql("Server=localhost")
                .ReplaceService<
                    IModelCacheKeyFactory,
                    TestDbContext.PerInstanceModelCacheKeyFactory>()
                .Options;

        protected override void ConfigureConventions(ModelConfigurationBuilder conventions) =>
            conventions.UseModelRules(rules ?? (builder => builder.NoShadowProperties()));

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            Models.Blog(modelBuilder);
    }
}
