namespace Hlibz.EntityFrameworkCore.ModelRules.Tests;

public sealed class ColumnFacetRulesTests
{
    [Fact]
    public void DecimalsHavePrecision_WithoutPrecision_ReportsIncludingComplexAndConverted()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                Models.Blog(model);
                model.Entity<Invoice>(invoice =>
                {
                    invoice.Property(x => x.Total).HasConversion(x => x.Amount, x => new Money(x));
                    invoice.OwnsOne(x => x.Contact);
                });
            },
            rules => rules.DecimalsHavePrecision());

        Assert.Equal(
            ["Blog.Address.Latitude", "Blog.Rating", "Invoice.Total"],
            violations.Select(violation => violation.Target).Order(StringComparer.Ordinal));
        Assert.All(violations, violation => Assert.Equal("MR003", violation.RuleId));
    }

    [Fact]
    public void DecimalsHavePrecision_WithFluentApiConventionOrColumnType_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<Blog>(blog =>
            {
                blog.Ignore(x => x.Posts);
                blog.Property(x => x.Rating).HasColumnType("numeric");
                blog.ComplexProperty(x => x.Address).Property(x => x.Latitude).HasPrecision(9, 6);
            }),
            rules => rules.DecimalsHavePrecision());

        Assert.Empty(violations);

        violations = TestDbContext.Validate(
            Models.Blog,
            rules => rules.DecimalsHavePrecision(),
            conventions => conventions.Properties<decimal>().HavePrecision(18, 2));

        Assert.Empty(violations);
    }

    [Fact]
    public void StringsHaveMaxLength_WithoutMaxLength_ReportsIncludingConvertedEnums()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                Models.Blog(model);
                model.Entity<Blog>().Property(x => x.Status).HasConversion<string>();
            },
            rules => rules.StringsHaveMaxLength());

        Assert.Equal(
            ["Blog.Address.City", "Blog.Name", "Blog.Status", "Blog.Subtitle", "Post.Title"],
            violations.Select(violation => violation.Target).Order(StringComparer.Ordinal));
        Assert.Contains(
            violations,
            violation => violation.Message.Contains("(converted from BlogStatus)", StringComparison.Ordinal));
    }

    [Fact]
    public void StringsHaveMaxLength_WithMaxLengthOrColumnType_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<Post>(post => post.Property(x => x.Title).HasColumnType("text")),
            rules => rules.StringsHaveMaxLength());

        Assert.Empty(violations);

        violations = TestDbContext.Validate(
            Models.Blog,
            rules => rules.StringsHaveMaxLength(),
            conventions => conventions.Properties<string>().HaveMaxLength(200));

        Assert.Empty(violations);
    }

    [Fact]
    public void EnumsStoredAsStrings_WithNumericStorage_ReportsEnum()
    {
        IReadOnlyList<ModelRuleViolation> violations =
            TestDbContext.Validate(Models.Blog, rules => rules.EnumsStoredAsStrings());

        ModelRuleViolation violation = Assert.Single(violations);
        Assert.Equal("MR006", violation.RuleId);
        Assert.Equal("Blog.Status", violation.Target);
    }

    [Fact]
    public void EnumsStoredAsStrings_WithStringConversion_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Blog,
            rules => rules.EnumsStoredAsStrings(),
            conventions => conventions.Properties<Enum>().HaveConversion<string>());

        Assert.Empty(violations);

        violations = TestDbContext.Validate(
            model =>
            {
                Models.Blog(model);
                model.Entity<Blog>().Property(x => x.Status).HasConversion<string>();
            },
            rules => rules.EnumsStoredAsStrings());

        Assert.Empty(violations);
    }

    [Fact]
    public void EnumConvention_WithStringConversionAndMaxLength_SatisfiesEnumAndStringRules()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                Models.Blog(model);
                model.Entity<Blog>(blog =>
                {
                    blog.Property(x => x.Name).HasMaxLength(100);
                    blog.Property(x => x.Subtitle).HasMaxLength(100);
                    blog.ComplexProperty(x => x.Address).Property(x => x.City).HasMaxLength(100);
                });
                model.Entity<Post>().Property(x => x.Title).HasMaxLength(100);
            },
            rules => rules.EnumsStoredAsStrings().StringsHaveMaxLength(),
            conventions => conventions.Properties<Enum>().HaveConversion<string>().HaveMaxLength(50));

        Assert.Empty(violations);
    }

    [Fact]
    public void NullabilityMatchesClr_WithMatchingAnnotations_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations =
            TestDbContext.Validate(Models.Blog, rules => rules.NullabilityMatchesClr());

        Assert.Empty(violations);
    }

    [Fact]
    public void NullabilityMatchesClr_WithRequirednessOverridden_ReportsBothMismatches()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                Models.Blog(model);
                model.Entity<Blog>(blog =>
                {
                    blog.Property(x => x.Name).IsRequired(false);
                    blog.Property(x => x.Subtitle).IsRequired();
                });
            },
            rules => rules.NullabilityMatchesClr());

        Assert.Equal(2, violations.Count);
        Assert.Contains(
            violations,
            violation => violation.Target == "Blog.Name"
                         && violation.Message.StartsWith("C# type is non-nullable", StringComparison.Ordinal));
        Assert.Contains(
            violations,
            violation => violation.Target == "Blog.Subtitle"
                         && violation.Message.StartsWith("C# type is nullable", StringComparison.Ordinal));
    }
}
