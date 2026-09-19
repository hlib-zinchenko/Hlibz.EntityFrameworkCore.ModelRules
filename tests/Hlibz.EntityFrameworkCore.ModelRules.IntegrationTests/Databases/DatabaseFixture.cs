using System.Data.Common;

using DotNet.Testcontainers.Containers;

namespace Hlibz.EntityFrameworkCore.ModelRules.IntegrationTests.Databases;

/// <summary>
/// One database server in Docker, shared by a test class. Every context it creates points at a
/// fresh database of its own, so tests never see each other's schema.
/// </summary>
public abstract class DatabaseFixture : IAsyncLifetime
{
    /// <summary>
    /// Names the database assigns itself and the model cannot configure, e.g. MySQL's
    /// <c>PRIMARY</c> key. The naming assertions skip these.
    /// </summary>
    public virtual IReadOnlySet<string> ServerAssignedNames { get; } = new HashSet<string>();

    /// <summary>
    /// A query returning <c>(kind, name)</c> rows for every table, column, constraint and index in
    /// the current database's default schema.
    /// </summary>
    protected abstract string IdentifiersSql { get; }

    protected abstract IDatabaseContainer Container { get; }

    public async ValueTask InitializeAsync() =>
        await ((IContainer)Container).StartAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        await ((IAsyncDisposable)Container).DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates a context on a new, uniquely named database. The database itself is created by
    /// <c>EnsureCreatedAsync</c>.
    /// </summary>
    internal IntegrationDbContext CreateContext(
        Action<ModelBuilder> model,
        Action<ModelRulesBuilder>? rules = null)
    {
        string connectionString = WithDatabase(
            Container.GetConnectionString(),
            $"mr_{Guid.NewGuid():N}"[..16]);

        return new IntegrationDbContext(
            options => UseProvider(options, connectionString),
            model,
            rules);
    }

    /// <summary>
    /// Every identifier the database actually created.
    /// </summary>
    internal async Task<IReadOnlyList<(string Kind, string Name)>> GetIdentifiersAsync(
        DbContext context)
    {
        IReadOnlyList<object?[]> rows = await QueryAsync(context, IdentifiersSql);
        return [.. rows.Select(row => ((string)row[0]!, (string)row[1]!))];
    }

    internal static async Task<IReadOnlyList<object?[]>> QueryAsync(DbContext context, string sql)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DbConnection connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            List<object?[]> rows = [];
            while (await reader.ReadAsync(cancellationToken))
            {
                object?[] row = new object?[reader.FieldCount];
                for (int index = 0; index < row.Length; index++)
                {
                    row[index] = await reader.IsDBNullAsync(index, cancellationToken)
                        ? null
                        : reader.GetValue(index);
                }

                rows.Add(row);
            }

            return rows;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    protected abstract void UseProvider(DbContextOptionsBuilder options, string connectionString);

    protected abstract string WithDatabase(string connectionString, string database);
}
