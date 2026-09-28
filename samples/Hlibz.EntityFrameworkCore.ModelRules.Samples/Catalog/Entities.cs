namespace Hlibz.EntityFrameworkCore.ModelRules.Samples.Catalog;

/// <summary>Marker for MR008: no cascade delete between two entities that both implement this.</summary>
public interface IAggregateRoot;

public enum BookGenre
{
    Fiction,
    NonFiction,
    Poetry,
}

public sealed class Author : IAggregateRoot
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<Book> Books { get; } = [];
}

// Deliberately has no AuthorId property: CatalogDbContext relates it to Author with WithOne(), so
// EF invents a shadow foreign key column instead of using a real one (MR001).
public sealed class Book
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public BookGenre Genre { get; set; }

    public string? Isbn { get; set; }
}

public sealed class Customer : IAggregateRoot
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public List<Order> Orders { get; } = [];
}

public sealed class Order : IAggregateRoot
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;
}
