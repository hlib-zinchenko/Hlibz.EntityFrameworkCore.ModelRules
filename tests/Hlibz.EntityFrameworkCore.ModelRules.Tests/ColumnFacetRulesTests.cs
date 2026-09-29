using Microsoft.EntityFrameworkCore.ChangeTracking;

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

    [Fact]
    public void NullabilityMatchesClr_WithPropertyBagEntity_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.SharedTypeEntity<Dictionary<string, object>>("Setting", setting =>
            {
                setting.IndexerProperty<int>("Id");
                setting.IndexerProperty<string>("Name").IsRequired();
                setting.IndexerProperty<string>("Note").IsRequired(false);
                setting.HasKey("Id");
            }),
            rules => rules.NullabilityMatchesClr());

        Assert.Empty(violations);
    }

    [Fact]
    public void ColumnFacetRules_WithForeignKeyToConfiguredKey_UseThePrincipalsFacets()
    {
        // EF Core gives a foreign key column its principal key's max length, precision and
        // conversion, so the dependent needn't repeat them.
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                model.Entity<Region>(region =>
                {
                    region.HasKey(x => x.Code);
                    region.Property(x => x.Code).HasMaxLength(2);
                    region.Property(x => x.TaxRate).HasPrecision(5, 4);
                    region.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
                    region.HasAlternateKey(x => x.TaxRate);
                    region.HasAlternateKey(x => x.Status);
                });
                model.Entity<Store>(store =>
                {
                    store.HasOne<Region>().WithMany().HasForeignKey(x => x.RegionCode);
                    store.HasOne<Region>()
                        .WithMany()
                        .HasForeignKey(x => x.RegionTaxRate)
                        .HasPrincipalKey(x => x.TaxRate);
                    store.HasOne<Region>()
                        .WithMany()
                        .HasForeignKey(x => x.RegionStatus)
                        .HasPrincipalKey(x => x.Status);
                });
            },
            rules => rules.StringsHaveMaxLength().DecimalsHavePrecision().EnumsStoredAsStrings());

        Assert.Empty(violations);
    }

    [Fact]
    public void StringsHaveMaxLength_WithForeignKeyToKeyWithColumnType_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                model.Entity<Region>(region =>
                {
                    region.HasKey(x => x.Code);
                    region.Property(x => x.Code).HasColumnType("text");
                    region.Ignore(x => x.TaxRate);
                    region.Ignore(x => x.Status);
                });
                model.Entity<Store>(store =>
                {
                    store.HasOne<Region>().WithMany().HasForeignKey(x => x.RegionCode);
                    store.Ignore(x => x.ExternalId);
                    store.Ignore(x => x.Number);
                });
            },
            rules => rules.StringsHaveMaxLength());

        Assert.Empty(violations);
    }

    [Fact]
    public void StringsHaveMaxLength_WithConverterSizeHint_ReportsOnlyUnboundedConversions()
    {
        // Guid and int converted to string get a length from their converters (36 and 64
        // characters); an enum converted to string doesn't, so its column is unbounded.
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<Store>(store =>
            {
                store.Property(x => x.RegionCode).HasMaxLength(2);
                store.Property(x => x.ExternalId).HasConversion<string>();
                store.Property(x => x.Number).HasConversion<string>();
                store.Property(x => x.RegionStatus).HasConversion<string>();
            }),
            rules => rules.StringsHaveMaxLength());

        Assert.Equal("Store.RegionStatus", Assert.Single(violations).Target);
    }

    [Fact]
    public void EnumsStoredAsStrings_WithNativePostgresEnum_ReportsOnlyScalarInJsonOwnedType()
    {
        using NativeEnumDbContext context = new();

        IReadOnlyList<ModelRuleViolation> violations =
            ModelRuleVerifier.Validate(context, rules => rules.EnumsStoredAsStrings());

        // Stored by name: the enum columns, the enum arrays, and the collection inside JSON.
        // Npgsql writes the single enum inside a JSON owned type as a number, though.
        Assert.Equal("DocumentMeta.Status", Assert.Single(violations).Target);
    }

    [Fact]
    public void EnumsStoredAsStrings_WithEnumCollection_ReportsListsAndArrays()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<Article>(),
            rules => rules.EnumsStoredAsStrings());

        Assert.Equal(
            ["Article.History", "Article.Statuses"],
            violations.Select(violation => violation.Target).Order(StringComparer.Ordinal));
        Assert.All(
            violations,
            violation => Assert.StartsWith(
                "collection of enum BlogStatus stores its elements as their underlying numbers.",
                violation.Message,
                StringComparison.Ordinal));
    }

    [Fact]
    public void EnumsStoredAsStrings_WithEnumCollectionElementsConverted_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<Article>(article =>
            {
                article.PrimitiveCollection(x => x.Statuses).ElementType().HasConversion<string>();
                article.PrimitiveCollection(x => x.History).ElementType().HasConversion<string>();
            }),
            rules => rules.EnumsStoredAsStrings());

        Assert.Empty(violations);
    }

    [Fact]
    public void EnumsStoredAsStrings_WithEnumConventionOnly_StillReportsEnumCollections()
    {
        // Properties<Enum>() configures enum properties, not the elements of a collection.
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<Article>(),
            rules => rules.EnumsStoredAsStrings(),
            conventions => conventions.Properties<Enum>().HaveConversion<string>());

        Assert.Equal(2, violations.Count);
    }

    [Fact]
    public void EnumsStoredAsStrings_WithEnumCollectionConvertedAsAWhole_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<Article>(article =>
            {
                article.Ignore(x => x.History);
                article.Property(x => x.Statuses).HasConversion(
                    statuses => string.Join(",", statuses),
                    joined => joined.Split(',', StringSplitOptions.None)
                        .Select(status => Enum.Parse<BlogStatus>(status))
                        .ToList(),
                    new ValueComparer<List<BlogStatus>>(
                        (left, right) => left!.SequenceEqual(right!),
                        statuses => statuses.Count,
                        statuses => statuses.ToList()));
            }),
            rules => rules.EnumsStoredAsStrings());

        Assert.Empty(violations);
    }

    [Fact]
    public void EnumsStoredAsStrings_WithEnumsInJsonOwnedType_ReportsThem()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.JsonDocuments,
            rules => rules.EnumsStoredAsStrings());

        Assert.Equal(
            [
                "DocumentMeta.Status: enum BlogStatus is stored as its underlying number.",
                "DocumentMeta.Statuses: collection of enum BlogStatus stores its elements as "
                + "their underlying numbers.",
            ],
            violations
                .Select(violation => $"{violation.Target}: {violation.Message.Split(". ")[0]}.")
                .Order(StringComparer.Ordinal));

        // Reported on the owned type, so an exclusion through its owner still reaches it.
        Assert.Empty(TestDbContext.Validate(
            Models.JsonDocuments,
            rules => rules.EnumsStoredAsStrings(except => except
                .Property<Document>(x => x.Meta.Status)
                .Property<Document>(x => x.Meta.Statuses))));
    }

    [Fact]
    public void EnumsStoredAsStrings_WithConvertedEnumsInJsonOwnedType_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<Document>().OwnsOne(x => x.Meta, meta =>
            {
                meta.ToJson();
                meta.PrimitiveCollection(x => x.Statuses).ElementType().HasConversion<string>();
            }),
            rules => rules.EnumsStoredAsStrings(),
            conventions => conventions.Properties<Enum>().HaveConversion<string>());

        Assert.Empty(violations);
    }

#if NET10_0_OR_GREATER
    [Fact]
    public void EnumsStoredAsStrings_WithEnumsInJsonComplexType_ReportsThemOnContainingEntity()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<Document>().ComplexProperty(x => x.Meta, meta => meta.ToJson()),
            rules => rules.EnumsStoredAsStrings());

        Assert.Equal(
            ["Document.Meta.Status", "Document.Meta.Statuses"],
            violations.Select(violation => violation.Target).Order(StringComparer.Ordinal));
        Assert.All(
            violations,
            violation => Assert.Equal(typeof(Document), violation.EntityClrType));
    }
#endif

    /// <summary>
    /// Maps <see cref="BlogStatus"/> to a PostgreSQL enum type, which stores the member names,
    /// for single enum columns and for arrays of the enum alike, and for a collection of the
    /// enum inside a JSON document. A single enum inside a JSON owned type is written as a
    /// number.
    /// </summary>
    private sealed class NativeEnumDbContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
#if NET9_0_OR_GREATER
            optionsBuilder.UseNpgsql(
                "Host=localhost",
                npgsql => npgsql.MapEnum<BlogStatus>("blog_status"));
#else
            Npgsql.NpgsqlDataSourceBuilder dataSource = new("Host=localhost");
            dataSource.MapEnum<BlogStatus>("blog_status");
            optionsBuilder.UseNpgsql(dataSource.Build());
#endif
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
#if !NET9_0_OR_GREATER
            modelBuilder.HasPostgresEnum<BlogStatus>("blog_status");
#endif
            modelBuilder.Entity<Region>().HasKey(x => x.Code);
            modelBuilder.Entity<Article>().Ignore(x => x.Ratings);
            Models.JsonDocuments(modelBuilder);
        }
    }
}
