using DotNet.Testcontainers.Containers;

using Npgsql;

using Testcontainers.PostgreSql;

namespace Hlibz.EntityFrameworkCore.ModelRules.IntegrationTests.Databases;

public sealed class PostgresFixture : DatabaseFixture
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17-alpine").Build();

    protected override IDatabaseContainer Container => _container;

    protected override string IdentifiersSql =>
        """
        select 'table', table_name::text from information_schema.tables
        where table_schema = current_schema()
        union all
        select 'column', column_name::text from information_schema.columns
        where table_schema = current_schema()
        union all
        select 'constraint', constraint_name::text from information_schema.table_constraints
        where table_schema = current_schema()
          and constraint_type in ('PRIMARY KEY', 'FOREIGN KEY', 'UNIQUE')
        union all
        select 'index', indexname::text from pg_indexes
        where schemaname = current_schema()
        """;

    protected override void UseProvider(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString);

    protected override string WithDatabase(string connectionString, string database) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Database = database }.ConnectionString;
}
