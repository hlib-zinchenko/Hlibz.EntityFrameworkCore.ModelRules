using DotNet.Testcontainers.Containers;

using MySql.Data.MySqlClient;

using Testcontainers.MySql;

namespace Hlibz.EntityFrameworkCore.ModelRules.IntegrationTests.Databases;

public sealed class MySqlFixture : DatabaseFixture
{
    // Root, because every test creates a database of its own.
    private readonly MySqlContainer _container =
        new MySqlBuilder("mysql:8.4").WithUsername("root").Build();

    /// <summary>
    /// MySQL always names a primary key (and its index) <c>PRIMARY</c>, whatever the model says.
    /// </summary>
    public override IReadOnlySet<string> ServerAssignedNames { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "PRIMARY" };

    protected override IDatabaseContainer Container => _container;

    protected override string IdentifiersSql =>
        """
        select 'table', TABLE_NAME from information_schema.TABLES
        where TABLE_SCHEMA = DATABASE()
        union all
        select 'column', COLUMN_NAME from information_schema.COLUMNS
        where TABLE_SCHEMA = DATABASE()
        union all
        select 'constraint', CONSTRAINT_NAME from information_schema.TABLE_CONSTRAINTS
        where TABLE_SCHEMA = DATABASE()
        union all
        select 'index', INDEX_NAME from information_schema.STATISTICS
        where TABLE_SCHEMA = DATABASE()
        """;

    protected override void UseProvider(DbContextOptionsBuilder options, string connectionString) =>
        options.UseMySQL(connectionString);

    protected override string WithDatabase(string connectionString, string database) =>
        new MySqlConnectionStringBuilder(connectionString) { Database = database }.ConnectionString;
}
