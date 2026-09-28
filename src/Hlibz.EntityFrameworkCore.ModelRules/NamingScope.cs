namespace Hlibz.EntityFrameworkCore.ModelRules;

/// <summary>
/// Which kinds of database identifiers a naming or identifier-length rule checks.
/// </summary>
[Flags]
public enum NamingScope
{
    /// <summary>
    /// No identifiers.
    /// </summary>
    None = 0,

    /// <summary>
    /// Schema names.
    /// </summary>
    Schemas = 1,

    /// <summary>
    /// Table and view names.
    /// </summary>
    Tables = 2,

    /// <summary>
    /// Column names, including columns of complex types and JSON container columns.
    /// </summary>
    Columns = 4,

    /// <summary>
    /// Primary and alternate key constraint names.
    /// </summary>
    Keys = 8,

    /// <summary>
    /// Foreign key constraint names.
    /// </summary>
    ForeignKeys = 16,

    /// <summary>
    /// Index names.
    /// </summary>
    Indexes = 32,

    /// <summary>
    /// Check constraint names.
    /// </summary>
    CheckConstraints = 64,

    /// <summary>
    /// Sequence names, including the sequences EF Core creates for TPC keys.
    /// </summary>
    Sequences = 128,

    /// <summary>
    /// Names of database functions mapped with <c>HasDbFunction</c>. Built-in functions are
    /// skipped.
    /// </summary>
    Functions = 256,

    /// <summary>
    /// Every kind of identifier, including kinds that later versions add.
    /// </summary>
    All = ~None,
}
