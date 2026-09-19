namespace Hlibz.EntityFrameworkCore.ModelRules.Tests;

/// <summary>
/// Model configurations shared by several tests.
/// </summary>
internal static class Models
{
    /// <summary>
    /// Blog with its Address complex property and a one-to-many to Post through a shadow foreign
    /// key, all on EF Core's default naming.
    /// </summary>
    public static void Blog(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Blog>(blog =>
        {
            blog.ComplexProperty(x => x.Address);
            blog.HasMany(x => x.Posts).WithOne();
        });

    /// <summary>
    /// A TPH hierarchy whose Dog.Weight decimal has no precision.
    /// </summary>
    public static void Dogs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Animal>();
        modelBuilder.Entity<Dog>();
        modelBuilder.Entity<Cat>();
    }

    /// <summary>
    /// Country -> Currency (both aggregate roots) and Country -> State (a child entity), with EF
    /// Core's default cascade delete on both.
    /// </summary>
    public static void Countries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Currency>()
            .HasMany(x => x.Countries)
            .WithOne(x => x.Currency)
            .HasForeignKey(x => x.CurrencyId);

        modelBuilder.Entity<Country>()
            .HasMany(x => x.States)
            .WithOne()
            .HasForeignKey(x => x.CountryId);
    }

    /// <summary>
    /// Passes every built-in rule: explicit snake_case names everywhere, precision and max
    /// lengths set, the enum stored as a string, and no cascade between aggregate roots.
    /// </summary>
    public static void Clean(ModelBuilder model)
    {
        model.Entity<Blog>(blog =>
        {
            blog.ToTable("blogs");
            blog.HasKey(x => x.Id).HasName("pk_blogs");
            blog.Property(x => x.Id).HasColumnName("id");
            blog.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
            blog.Property(x => x.Subtitle).HasColumnName("subtitle").HasMaxLength(100);
            blog.Property(x => x.Rating).HasColumnName("rating").HasPrecision(5, 2);
            blog.Property(x => x.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(20);
            blog.ComplexProperty(x => x.Address, address =>
            {
                address.Property(x => x.City).HasColumnName("address_city").HasMaxLength(100);
                address.Property(x => x.Latitude)
                    .HasColumnName("address_latitude")
                    .HasPrecision(9, 6);
            });
            blog.Ignore(x => x.Posts);
        });

        model.Entity<Currency>(currency =>
        {
            currency.ToTable("currencies");
            currency.HasKey(x => x.Id).HasName("pk_currencies");
            currency.Property(x => x.Id).HasColumnName("id");
        });

        model.Entity<Country>(country =>
        {
            country.ToTable("countries");
            country.HasKey(x => x.Id).HasName("pk_countries");
            country.Property(x => x.Id).HasColumnName("id");
            country.Property(x => x.CurrencyId).HasColumnName("currency_id");
            country.Ignore(x => x.States);
            country.HasOne(x => x.Currency)
                .WithMany(x => x.Countries)
                .HasForeignKey(x => x.CurrencyId)
                .HasConstraintName("fk_countries_currencies_currency_id")
                .OnDelete(DeleteBehavior.Restrict);
            country.HasIndex(x => x.CurrencyId).HasDatabaseName("ix_countries_currency_id");
        });
    }
}
