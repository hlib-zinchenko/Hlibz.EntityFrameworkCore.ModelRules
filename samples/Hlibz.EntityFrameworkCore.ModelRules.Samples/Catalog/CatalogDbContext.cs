using Hlibz.EntityFrameworkCore.ModelRules;

using Microsoft.EntityFrameworkCore;

namespace Hlibz.EntityFrameworkCore.ModelRules.Samples.Catalog;

/// <summary>
/// A small bookstore model, registered with every built-in rule, that deliberately breaks each one
/// exactly once - the kind of drift real projects accumulate a little at a time rather than all at
/// once.
/// </summary>
public sealed class CatalogDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        // Never actually opened: building the model - all UseModelRules and ModelRules.Verify
        // need - doesn't connect to a database.
        optionsBuilder.UseNpgsql("Host=localhost;Database=catalog");

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.UseModelRules(rules => rules
            .NoShadowProperties()
            .NamesFollow(NamingStyle.SnakeCase)
            .DecimalsHavePrecision()
            .StringsHaveMaxLength()
            .NullabilityMatchesClr()
            .EnumsStoredAsStrings()
            .SingleSchema("catalog")
            .NoCascadeDeleteAcrossAggregates<IAggregateRoot>()
            .MaxIdentifierLength(63));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("catalog");

        modelBuilder.Entity<Author>(author =>
        {
            author.ToTable("authors");
            author.HasKey(x => x.Id).HasName("pk_authors");
            author.Property(x => x.Id).HasColumnName("id");
            author.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);

            // WithOne() with no HasForeignKey: Book has no AuthorId property to point at, so EF
            // creates a shadow "AuthorId" column instead of using a real one (MR001).
            author.HasMany(x => x.Books).WithOne();
        });

        modelBuilder.Entity<Book>(book =>
        {
            book.ToTable("books");

            // Well past PostgreSQL's 63-character limit. EF Core shortens the names it generates
            // itself, but not one set explicitly like this - PostgreSQL would silently truncate it
            // instead (MR009).
            book.HasKey(x => x.Id)
                .HasName("pk_books_with_an_excessively_long_primary_key_constraint_name_nobody_needs");
            book.Property(x => x.Id).HasColumnName("id");

            // No HasMaxLength: falls back to the provider's default - unconstrained on
            // PostgreSQL (MR004).
            book.Property(x => x.Title).HasColumnName("title");

            // No HasPrecision: falls back to the provider's default too (MR003).
            book.Property(x => x.Price).HasColumnName("price");

            // No HasConversion<string>: stored as its underlying number, so inserting a new value
            // in the middle of the enum would silently change what every existing row means (MR006).
            book.Property(x => x.Genre).HasColumnName("genre");

            book.Property(x => x.Isbn)
                .HasColumnName("ISBN") // Not snake_case (MR002).
                .HasMaxLength(20)
                .IsRequired(); // Isbn is `string?` in C#, but this makes the column NOT NULL (MR005).
        });

        modelBuilder.Entity<Customer>(customer =>
        {
            customer.ToTable("customers");
            customer.HasKey(x => x.Id).HasName("pk_customers");
            customer.Property(x => x.Id).HasColumnName("id");
            customer.Property(x => x.Email).HasColumnName("email").HasMaxLength(320);
        });

        modelBuilder.Entity<Order>(order =>
        {
            // A different schema than the rest of the model's tables (MR007).
            order.ToTable("orders", schema: "sales");
            order.HasKey(x => x.Id).HasName("pk_orders");
            order.Property(x => x.Id).HasColumnName("id");
            order.Property(x => x.CustomerId).HasColumnName("customer_id");

            // No OnDelete(...): EF Core defaults a required relationship to cascade, and Order and
            // Customer are both aggregate roots, so deleting a customer would delete their orders
            // too (MR008).
            order.HasOne(x => x.Customer)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.CustomerId)
                .HasConstraintName("fk_orders_customers_customer_id");

            order.HasIndex(x => x.CustomerId).HasDatabaseName("ix_orders_customer_id");
        });
    }
}
