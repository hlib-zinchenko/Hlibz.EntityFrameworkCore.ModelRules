namespace Hlibz.EntityFrameworkCore.ModelRules.Tests;

public interface IAggregateRoot;

public interface ISoftDeletable
{
    bool IsDeleted { get; }
}

public enum BlogStatus
{
    Draft,
    Published,
}

public sealed class Blog : IAggregateRoot
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Subtitle { get; set; }

    public decimal Rating { get; set; }

    public BlogStatus Status { get; set; }

    public Address Address { get; set; } = new();

    public List<Post> Posts { get; } = [];
}

public sealed class Address
{
    public string City { get; set; } = string.Empty;

    public decimal Latitude { get; set; }
}

/// <summary>
/// Deliberately has no BlogId property, so EF creates a shadow foreign key for it.
/// </summary>
public sealed class Post
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;
}

public sealed class Currency : IAggregateRoot
{
    public int Id { get; set; }

    public List<Country> Countries { get; } = [];
}

public sealed class Country : IAggregateRoot
{
    public int Id { get; set; }

    public int CurrencyId { get; set; }

    public Currency Currency { get; set; } = null!;

    public List<State> States { get; } = [];
}

public sealed class State
{
    public int Id { get; set; }

    public int CountryId { get; set; }
}

public abstract class Animal
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class Dog : Animal
{
    public decimal Weight { get; set; }
}

public sealed class Cat : Animal
{
    public bool Indoor { get; set; }
}

public readonly record struct Money(decimal Amount);

public sealed class Invoice
{
    public int Id { get; set; }

    public Money Total { get; set; }

    public Contact Contact { get; set; } = new();
}

public sealed class Contact
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// A TPH hierarchy whose derived type sorts before its base type, so tests catch identifiers
/// being attributed to whichever entity type happens to come first.
/// </summary>
public abstract class Vehicle
{
    public int Id { get; set; }
}

public sealed class Car : Vehicle
{
    public int Seats { get; set; }
}

/// <summary>
/// A TPC hierarchy of aggregate roots: each concrete account gets a table of its own, with its
/// own copies of the foreign key and index declared on the abstract base.
/// </summary>
public abstract class Account : IAggregateRoot
{
    public int Id { get; set; }

    public int CurrencyId { get; set; }
}

public sealed class SavingsAccount : Account
{
    public decimal Rate { get; set; }
}

public sealed class CheckingAccount : Account
{
    public decimal Overdraft { get; set; }
}

/// <summary>
/// A method mapped to a database function with <c>HasDbFunction</c>. Never called.
/// </summary>
public static class ReportFunctions
{
    public static int OrderTotal(int orderId) => throw new NotSupportedException();
}

public sealed class Comment : ISoftDeletable
{
    public int Id { get; set; }

    public bool IsDeleted { get; set; }
}

/// <summary>
/// A TPH hierarchy where only the derived type is soft-deletable, so its filter has to go on a
/// root that doesn't implement the marker itself.
/// </summary>
public class Note
{
    public int Id { get; set; }
}

public sealed class ArchivedNote : Note, ISoftDeletable
{
    public bool IsDeleted { get; set; }
}

/// <summary>
/// Two aggregate roots in a many-to-many relationship, navigable from both sides.
/// </summary>
public sealed class Author : IAggregateRoot
{
    public int Id { get; set; }

    public List<Book> Books { get; } = [];
}

public sealed class Book : IAggregateRoot
{
    public int Id { get; set; }

    public List<Author> Authors { get; } = [];
}
