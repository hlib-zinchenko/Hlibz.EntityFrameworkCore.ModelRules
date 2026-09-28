namespace Hlibz.EntityFrameworkCore.ModelRules.Samples.Catalog;

/// <summary>
/// Marker for the aggregate rules: roots refer to each other by key only (MR012), never delete
/// each other by cascade (MR008), and each has a concurrency token (MR013).
/// </summary>
public interface IAggregateRoot;

/// <summary>Marker for MR010: every entity implementing this needs a query filter.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }
}

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

    /// <summary>PostgreSQL's xmin system column, used as the row version (MR013).</summary>
    public uint Version { get; set; }

    public List<Book> Books { get; } = [];
}

public sealed class Book : ISoftDeletable
{
    public int Id { get; set; }

    public int AuthorId { get; set; }

    public string Title { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public BookGenre Genre { get; set; }

    public string? Isbn { get; set; }

    public bool IsDeleted { get; set; }
}

public sealed class Customer : IAggregateRoot
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public uint Version { get; set; }
}

public sealed class Order : IAggregateRoot
{
    public int Id { get; set; }

    /// <summary>
    /// Customer is another aggregate root, so Order refers to it by key only, with no navigation
    /// (MR012).
    /// </summary>
    public int CustomerId { get; set; }

    public uint Version { get; set; }
}
