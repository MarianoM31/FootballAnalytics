using Dapper;
using Microsoft.Data.SqlClient;

namespace FootballAnalytics.DatabaseMigrator;

public sealed class DatabaseMigratorService
{
    private readonly string connectionString;
    private readonly MigrationCatalog catalog;

    public DatabaseMigratorService(string connectionString, MigrationCatalog catalog)
    {
        this.connectionString = connectionString;
        this.catalog = catalog;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("The connection string must specify a database name.");
        }

        await EnsureDatabaseExistsAsync(builder, databaseName, cancellationToken);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureMigrationHistoryAsync(connection, cancellationToken);

        var appliedMigrationIds = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT MigrationId FROM dbo.SchemaMigrations;", cancellationToken: cancellationToken)))
            .ToHashSet(StringComparer.Ordinal);
        var pendingMigrations = catalog.Discover()
            .Where(migration => !appliedMigrationIds.Contains(migration.Id))
            .ToList();

        if (pendingMigrations.Count == 0)
        {
            Console.WriteLine("No pending migrations.");
            return;
        }

        foreach (var migration in pendingMigrations)
        {
            Console.WriteLine($"Applying {migration.Name}...");
            var sql = await File.ReadAllTextAsync(migration.Path, cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await connection.ExecuteAsync(new CommandDefinition(sql, transaction: transaction, cancellationToken: cancellationToken));
                await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO dbo.SchemaMigrations (MigrationId, MigrationName, AppliedAtUtc) VALUES (@Id, @Name, SYSUTCDATETIME());",
                    new { migration.Id, migration.Name }, transaction, cancellationToken: cancellationToken));
                await transaction.CommitAsync(cancellationToken);
                Console.WriteLine($"{migration.Name} applied successfully.");
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
    }

    private static async Task EnsureDatabaseExistsAsync(SqlConnectionStringBuilder configuredBuilder, string databaseName, CancellationToken cancellationToken)
    {
        var masterBuilder = new SqlConnectionStringBuilder(configuredBuilder.ConnectionString) { InitialCatalog = "master" };
        await using var masterConnection = new SqlConnection(masterBuilder.ConnectionString);
        await masterConnection.OpenAsync(cancellationToken);
        var exists = await masterConnection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT DB_ID(@DatabaseName);", new { DatabaseName = databaseName }, cancellationToken: cancellationToken));

        if (exists.HasValue)
        {
            Console.WriteLine($"Database {databaseName} already exists.");
            return;
        }

        var quotedDatabaseName = $"[{databaseName.Replace("]", "]]", StringComparison.Ordinal)}]";
        await masterConnection.ExecuteAsync(new CommandDefinition($"CREATE DATABASE {quotedDatabaseName};", cancellationToken: cancellationToken));
        Console.WriteLine($"Database {databaseName} created.");
    }

    private static Task EnsureMigrationHistoryAsync(SqlConnection connection, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            IF OBJECT_ID(N'dbo.SchemaMigrations', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SchemaMigrations
                (
                    MigrationId nvarchar(50) NOT NULL CONSTRAINT PK_SchemaMigrations PRIMARY KEY,
                    MigrationName nvarchar(255) NOT NULL,
                    AppliedAtUtc datetime2 NOT NULL
                );
            END;
            """,
            cancellationToken: cancellationToken));
}
