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

#if !NET10_0_OR_GREATER
    /// <summary>
    /// Pomelo's MySQL provider, the most widely used one. It has no EF Core 10 release yet.
    /// </summary>
    Pomelo,
#endif
}
