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

/// <summary>
/// An entity split across two tables with <c>SplitToTable</c>: Bio and Website go to a second
/// table.
/// </summary>
public sealed class Profile
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Bio { get; set; } = string.Empty;

    public string Website { get; set; } = string.Empty;
}

/// <summary>
/// Owns one address (in the shipment's table) and many parcels (in a table of their own), each
/// parcel owning an address in turn.
/// </summary>
public sealed class Shipment
{
    public int Id { get; set; }

    public ShippingAddress Destination { get; set; } = new();

    public List<Parcel> Parcels { get; } = [];
}

public sealed class Parcel
{
    public string Label { get; set; } = string.Empty;

    public ShippingAddress ReturnTo { get; set; } = new();
}

public sealed class ShippingAddress
{
    public string Street { get; set; } = string.Empty;
}

/// <summary>
/// A lookup row keyed by a two-letter code, referenced from <see cref="Store"/> by that code.
/// </summary>
public sealed class Region
{
    public string Code { get; set; } = string.Empty;

    public decimal TaxRate { get; set; }

    public BlogStatus Status { get; set; }
}

public sealed class Store
{
    public int Id { get; set; }

    public string RegionCode { get; set; } = string.Empty;

    public decimal RegionTaxRate { get; set; }

    public BlogStatus RegionStatus { get; set; }

    public Guid ExternalId { get; set; }

    public int Number { get; set; }
}

/// <summary>
/// Primitive collections: two of enums, which EF Core stores as numbers by default, and one of
/// plain integers.
/// </summary>
public sealed class Article
{
    public int Id { get; set; }

    public List<BlogStatus> Statuses { get; set; } = [];

    public BlogStatus[] History { get; set; } = [];

    public List<int> Ratings { get; set; } = [];
}

/// <summary>
/// Metadata stored as a JSON document, holding an enum and a collection of enums, which EF Core
/// writes into the document as numbers by default.
/// </summary>
public sealed class Document
{
    public int Id { get; set; }

    public DocumentMeta Meta { get; set; } = new();
}

public sealed class DocumentMeta
{
    public string Title { get; set; } = string.Empty;

    public BlogStatus Status { get; set; }

    public List<BlogStatus> Statuses { get; set; } = [];
}
