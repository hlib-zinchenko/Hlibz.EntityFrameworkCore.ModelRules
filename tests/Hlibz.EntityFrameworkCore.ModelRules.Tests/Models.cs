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
}
