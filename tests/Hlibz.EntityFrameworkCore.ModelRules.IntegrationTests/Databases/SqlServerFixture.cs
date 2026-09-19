using DotNet.Testcontainers.Containers;

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

namespace Hlibz.EntityFrameworkCore.ModelRules.IntegrationTests.Databases;

public sealed class SqlServerFixture : DatabaseFixture
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    protected override IDatabaseContainer Container => _container;

    protected override string IdentifiersSql =>
        """
        select 'table', TABLE_NAME from INFORMATION_SCHEMA.TABLES
        where TABLE_SCHEMA = SCHEMA_NAME()
        union all
        select 'column', COLUMN_NAME from INFORMATION_SCHEMA.COLUMNS
        where TABLE_SCHEMA = SCHEMA_NAME()
        union all
        select 'constraint', CONSTRAINT_NAME from INFORMATION_SCHEMA.TABLE_CONSTRAINTS
        where TABLE_SCHEMA = SCHEMA_NAME()
        union all
        select 'index', i.name from sys.indexes i
        join sys.tables t on t.object_id = i.object_id
        where i.name is not null and t.schema_id = SCHEMA_ID()
        union all
        select 'default constraint', d.name from sys.default_constraints d
        join sys.tables t on t.object_id = d.parent_object_id
        where t.schema_id = SCHEMA_ID()
        """;

    protected override void UseProvider(DbContextOptionsBuilder options, string connectionString) =>
        options.UseSqlServer(connectionString);

    protected override string WithDatabase(string connectionString, string database) =>
        new SqlConnectionStringBuilder(connectionString) { InitialCatalog = database }.ConnectionString;
}
