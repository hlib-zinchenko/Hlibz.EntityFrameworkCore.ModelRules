using Hlibz.EntityFrameworkCore.ModelRules;

using Microsoft.EntityFrameworkCore;

namespace Hlibz.EntityFrameworkCore.ModelRules.Samples.Catalog;

/// <summary>
/// A small bookstore model, registered with every built-in rule, that follows every one of them.
/// Points at the PostgreSQL instance <c>docker-compose.yml</c> starts, so <c>dotnet ef</c> and
/// <c>dotnet run</c> can actually reach a database.
/// </summary>
public sealed class CatalogDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5433;Database=catalog;Username=catalog;Password=catalog");

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

            // Book.AuthorId is a real property, so this is a normal foreign key rather than a
            // shadow one. Author owns Book, and Book isn't an aggregate root, so the default
            // cascade delete here is the correct behavior, not an MR008 violation.
            author.HasMany(x => x.Books)
                .WithOne()
                .HasForeignKey(x => x.AuthorId)
                .HasConstraintName("fk_books_authors_author_id");
        });

        modelBuilder.Entity<Book>(book =>
        {
            book.ToTable("books");
            book.HasKey(x => x.Id).HasName("pk_books");
            book.Property(x => x.Id).HasColumnName("id");
            book.Property(x => x.AuthorId).HasColumnName("author_id");
            book.Property(x => x.Title).HasColumnName("title").HasMaxLength(300);
            book.Property(x => x.Price).HasColumnName("price").HasPrecision(10, 2);
            book.Property(x => x.Genre)
                .HasColumnName("genre")
                .HasConversion<string>()
                .HasMaxLength(20);
            book.Property(x => x.Isbn).HasColumnName("isbn").HasMaxLength(20);

            book.HasIndex(x => x.AuthorId).HasDatabaseName("ix_books_author_id");
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
            order.ToTable("orders");
            order.HasKey(x => x.Id).HasName("pk_orders");
            order.Property(x => x.Id).HasColumnName("id");
            order.Property(x => x.CustomerId).HasColumnName("customer_id");

            // Order and Customer are both aggregate roots, so this relationship is restricted
            // rather than left to EF Core's default cascade (MR008).
            order.HasOne(x => x.Customer)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.CustomerId)
                .HasConstraintName("fk_orders_customers_customer_id")
                .OnDelete(DeleteBehavior.Restrict);

            order.HasIndex(x => x.CustomerId).HasDatabaseName("ix_orders_customer_id");
        });
    }
}
