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
    /// Passes every built-in rule: explicit snake_case names everywhere (a check constraint
    /// included), precision and max lengths set, the enum stored as a string, and aggregate roots
    /// referring to each other by key only, with no cascade delete between them.
    /// </summary>
    public static void Clean(ModelBuilder model)
    {
        model.Entity<Blog>(blog =>
        {
            blog.ToTable("blogs", table => table.HasCheckConstraint("ck_blogs_rating", "rating >= 0"));
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
            currency.Ignore(x => x.Countries);
        });

        model.Entity<Country>(country =>
        {
            country.ToTable("countries");
            country.HasKey(x => x.Id).HasName("pk_countries");
            country.Property(x => x.Id).HasColumnName("id");
            country.Property(x => x.CurrencyId).HasColumnName("currency_id");
            country.Ignore(x => x.States);
            country.Ignore(x => x.Currency);
            country.HasOne<Currency>()
                .WithMany()
                .HasForeignKey(x => x.CurrencyId)
                .HasConstraintName("fk_countries_currencies_currency_id")
                .OnDelete(DeleteBehavior.Restrict);
            country.HasIndex(x => x.CurrencyId).HasDatabaseName("ix_countries_currency_id");
        });
    }

    /// <summary>
    /// The TPC Account hierarchy, referencing Currency, with EF Core's default names.
    /// </summary>
    public static void Accounts(ModelBuilder model)
    {
        model.Entity<Currency>().Ignore(x => x.Countries);
        model.Entity<Account>(account =>
        {
            account.UseTpcMappingStrategy();
            account.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId);
        });
        model.Entity<SavingsAccount>().Property(x => x.Rate).HasPrecision(5, 2);
        model.Entity<CheckingAccount>().Property(x => x.Overdraft).HasPrecision(10, 2);
    }

    /// <summary>
    /// Maps <see cref="ReportFunctions.OrderTotal"/> as a database function.
    /// </summary>
    public static void OrderTotalFunction(ModelBuilder model) =>
        model.HasDbFunction(typeof(ReportFunctions).GetMethod(nameof(ReportFunctions.OrderTotal))!);

    /// <summary>
    /// Profile split across two tables: the snake_case <c>profiles</c>, and
    /// <c>ProfileDetails</c> with Bio and Website, on EF Core's default names.
    /// </summary>
    public static void Profiles(ModelBuilder model) =>
        model.Entity<Profile>(profile =>
        {
            profile.ToTable("profiles");
            profile.SplitToTable("ProfileDetails", table =>
            {
                table.Property(x => x.Bio);
                table.Property(x => x.Website);
            });
        });

    /// <summary>
    /// Shipment owning a destination in its own table, and parcels in a table of their own that
    /// each own a return address. No string has a max length.
    /// </summary>
    public static void Shipments(ModelBuilder model) =>
        model.Entity<Shipment>(shipment =>
        {
            shipment.OwnsOne(x => x.Destination);
            shipment.OwnsMany(x => x.Parcels, parcel => parcel.OwnsOne(x => x.ReturnTo));
        });

    /// <summary>
    /// Document with its metadata in a JSON column, as an owned type.
    /// </summary>
    public static void JsonDocuments(ModelBuilder model) =>
        model.Entity<Document>().OwnsOne(x => x.Meta, meta => meta.ToJson());
}
