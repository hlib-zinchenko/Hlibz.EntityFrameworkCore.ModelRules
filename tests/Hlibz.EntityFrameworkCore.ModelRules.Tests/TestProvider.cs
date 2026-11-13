namespace Hlibz.EntityFrameworkCore.ModelRules.Tests;

/// <summary>
/// The EF Core providers every model can be built with. Building a model never opens a
/// connection, so none of them needs a running database.
/// </summary>
public enum TestProvider
{
    Npgsql,
    SqlServer,
    Sqlite,

    /// <summary>
    /// Oracle's official MySQL provider (MySql.EntityFrameworkCore).
    /// </summary>
    MySql,
}
