using System.Text.RegularExpressions;

namespace Hlibz.EntityFrameworkCore.ModelRules.Tests;

public sealed class NamesFollowTests
{
    [Fact]
    public void NamesFollow_WithEfDefaultNames_ReportsEveryIdentifierKind()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Blog,
            rules => rules.NamesFollow(NamingStyle.SnakeCase));

        string[] messages = [.. violations.Select(violation => violation.Message)];
        Assert.Contains("table name 'Blog' is not snake_case.", messages);
        Assert.Contains("column name 'Name' is not snake_case.", messages);
        Assert.Contains("column name 'Address_City' is not snake_case.", messages);
        Assert.Contains("primary key name 'PK_Blog' is not snake_case.", messages);
        Assert.Contains("foreign key name 'FK_Post_Blog_BlogId' is not snake_case.", messages);
        Assert.Contains("index name 'IX_Post_BlogId' is not snake_case.", messages);
        Assert.All(violations, violation => Assert.Equal("MR002", violation.RuleId));
    }

    [Fact]
    public void NamesFollow_WithComplexTypeColumn_ReportsContainingEntityWithDottedPath()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Blog,
            rules => rules.NamesFollow(NamingStyle.SnakeCase, NamingScope.Columns));

        ModelRuleViolation violation = Assert.Single(
            violations,
            violation => violation.Message.Contains("Address_City", StringComparison.Ordinal));
        Assert.Equal(typeof(Blog), violation.EntityClrType);
        Assert.Equal("Address.City", violation.MemberPath);
    }

    [Fact]
    public void NamesFollow_WithAllNamesSnakeCase_ReportsNothing()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<State>(state =>
            {
                state.ToTable("states", "geo");
                state.HasKey(x => x.Id).HasName("pk_states");
                state.Property(x => x.Id).HasColumnName("id");
                state.Property(x => x.CountryId).HasColumnName("country_id");
                state.HasIndex(x => x.CountryId).HasDatabaseName("ix_states_country_id");
            }),
            rules => rules.NamesFollow(NamingStyle.SnakeCase));

        Assert.Empty(violations);
    }

    [Fact]
    public void NamesFollow_WithAcronymsAndDigits_ChecksShapeNotDerivation()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<State>(state =>
            {
                state.ToTable("states");
                state.Property(x => x.Id).HasColumnName("ip_address");
                state.Property(x => x.CountryId).HasColumnName("line2text");
            }),
            rules => rules.NamesFollow(NamingStyle.SnakeCase, NamingScope.Columns));

        Assert.Empty(violations);
    }

    [Fact]
    public void NamesFollow_WithTablesScope_ChecksOnlyTableNames()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Blog,
            rules => rules.NamesFollow(NamingStyle.SnakeCase, NamingScope.Tables));

        Assert.Equal(
            ["table name 'Blog' is not snake_case.", "table name 'Post' is not snake_case."],
            violations.Select(violation => violation.Message).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void NamesFollow_WithTphHierarchy_ReportsSharedTableOnceAgainstBaseType()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                model.Entity<Vehicle>().ToTable("Vehicles", "Fleet");
                model.Entity<Car>();
            },
            rules => rules.NamesFollow(
                NamingStyle.SnakeCase,
                NamingScope.Tables | NamingScope.Schemas));

        Assert.Equal(2, violations.Count);
        Assert.All(violations, violation => Assert.Equal(typeof(Vehicle), violation.EntityClrType));
    }

    [Fact]
    public void NamesFollow_WithPascalCase_ReportsEfDefaultConstraintNames()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Blog,
            rules => rules.NamesFollow(NamingStyle.PascalCase, NamingScope.Tables | NamingScope.Keys));

        ModelRuleViolation[] keyViolations =
            [.. violations.Where(violation => violation.Message.StartsWith("primary key", StringComparison.Ordinal))];
        Assert.Equal(2, keyViolations.Length);
        Assert.Equal(2, violations.Count);
    }

    [Fact]
    public void NamesFollow_WithCustomPattern_ChecksNamesAgainstThePattern()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Blog,
            rules => rules.NamesFollow(new Regex("^[A-Z][a-z]+$"), NamingScope.Tables));

        Assert.Empty(violations);
    }

    [Fact]
    public void NamesFollow_WithJsonOwnedType_ChecksOnlyContainerColumn()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<Invoice>(invoice =>
            {
                invoice.ToTable("invoices");
                invoice.HasKey(x => x.Id).HasName("pk_invoices");
                invoice.Property(x => x.Id).HasColumnName("id");
                invoice.Property(x => x.Total)
                    .HasConversion(x => x.Amount, x => new Money(x))
                    .HasColumnName("total");
                invoice.OwnsOne(x => x.Contact, contact => contact.ToJson("ContactInfo"));
            }),
            rules => rules.NamesFollow(NamingStyle.SnakeCase));

        ModelRuleViolation violation = Assert.Single(violations);
        Assert.Equal("column name 'ContactInfo' is not snake_case.", violation.Message);
        Assert.Equal(typeof(Invoice), violation.EntityClrType);
        Assert.Equal("Contact", violation.MemberPath);
    }

    [Fact]
    public void NamesFollow_WithSequencesAndFunctions_ReportsThemWithoutEntityType()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                Models.Blog(model);
                model.HasSequence<long>("OrderNumbers", "sales");
                model.HasSequence<long>("invoice_numbers", "sales");
                Models.OrderTotalFunction(model);
            },
            rules => rules.NamesFollow(
                NamingStyle.SnakeCase,
                NamingScope.Sequences | NamingScope.Functions | NamingScope.Schemas));

        Assert.Equal(
            ["function OrderTotal", "sequence sales.OrderNumbers"],
            violations.Select(violation => violation.Target).Order(StringComparer.Ordinal));
        Assert.All(violations, violation => Assert.Null(violation.EntityClrType));
        Assert.Contains(
            violations,
            violation => violation.Message == "sequence name 'OrderNumbers' is not snake_case.");
    }

    [Fact]
    public void NamesFollow_WithSchemaOnlyASequenceUses_ReportsSchemaWithoutEntityType()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model =>
            {
                Models.Blog(model);
                model.HasSequence<long>("order_numbers", "Sales");
            },
            rules => rules.NamesFollow(NamingStyle.SnakeCase, NamingScope.Schemas));

        ModelRuleViolation violation = Assert.Single(violations);
        Assert.Equal("schema Sales", violation.Target);
        Assert.Equal("schema name 'Sales' is not snake_case.", violation.Message);
    }

    [Fact]
    public void NamesFollow_WithCheckConstraint_ReportsItAgainstEntityType()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<State>().ToTable(
                table => table.HasCheckConstraint("CK_State_CountryId", "country_id > 0")),
            rules => rules.NamesFollow(NamingStyle.SnakeCase, NamingScope.CheckConstraints));

        ModelRuleViolation violation = Assert.Single(violations);
        Assert.Equal("State", violation.Target);
        Assert.Equal(
            "check constraint name 'CK_State_CountryId' is not snake_case.",
            violation.Message);
    }

    [Fact]
    public void NamesFollow_WithTpcHierarchy_ChecksEveryTablesForeignKeyAndIndex()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Accounts,
            rules => rules.NamesFollow(
                NamingStyle.SnakeCase,
                NamingScope.ForeignKeys | NamingScope.Indexes));

        Assert.Equal(
            [
                "foreign key name 'FK_CheckingAccount_Currency_CurrencyId' is not snake_case.",
                "foreign key name 'FK_SavingsAccount_Currency_CurrencyId' is not snake_case.",
                "index name 'IX_CheckingAccount_CurrencyId' is not snake_case.",
                "index name 'IX_SavingsAccount_CurrencyId' is not snake_case.",
            ],
            violations.Select(violation => violation.Message).Order(StringComparer.Ordinal));
        Assert.All(violations, violation => Assert.Equal("Account.CurrencyId", violation.Target));
    }

    [Fact]
    public void NamesFollow_WithTpcHierarchy_ReportsKeySequence()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            Models.Accounts,
            rules => rules.NamesFollow(NamingStyle.SnakeCase, NamingScope.Sequences));

        Assert.Equal("sequence AccountSequence", Assert.Single(violations).Target);
    }

    [Fact]
    public void NamesFollow_WithOwnedTypeInOwnersTable_ReportsSharedNamesAgainstOwner()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            model => model.Entity<Invoice>(invoice =>
            {
                invoice.Property(x => x.Total).HasConversion(x => x.Amount, x => new Money(x));
                invoice.OwnsOne(x => x.Contact);
            }),
            rules => rules.NamesFollow(
                NamingStyle.SnakeCase,
                NamingScope.Columns | NamingScope.Keys | NamingScope.ForeignKeys));

        // No foreign key: the owned type shares its owner's row, so the database has none.
        Assert.Equal(
            [
                "Contact.Email: column name 'Contact_Email' is not snake_case.",
                "Invoice.Id: column name 'Id' is not snake_case.",
                "Invoice.Id: primary key name 'PK_Invoice' is not snake_case.",
                "Invoice.Total: column name 'Total' is not snake_case.",
            ],
            violations
                .Select(violation => $"{violation.Target}: {violation.Message}")
                .Order(StringComparer.Ordinal));
    }
}
