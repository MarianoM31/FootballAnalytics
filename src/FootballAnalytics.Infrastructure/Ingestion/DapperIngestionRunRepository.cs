using Dapper;
using FootballAnalytics.Application.Ingestion;
using FootballAnalytics.Application.Persistence;
using Microsoft.Data.SqlClient;

namespace FootballAnalytics.Infrastructure.Ingestion;

public sealed class DapperIngestionRunRepository(IDbConnectionFactory connectionFactory) : IIngestionRunRepository
{
    public async Task<Guid> StartAsync(string providerCode, DateTimeOffset startedAtUtc, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await using var connection = (SqlConnection)connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.IngestionRuns
                (Id, Provider, StartedAtUtc, Status, CompetitionsProcessed, SeasonsProcessed, TeamsProcessed, FixturesProcessed)
            VALUES
                (@Id, @ProviderCode, @StartedAtUtc, @Status, 0, 0, 0, 0);
            """,
            new { Id = id, ProviderCode = providerCode, StartedAtUtc = startedAtUtc.UtcDateTime, Status = (byte)IngestionRunStatus.Running },
            cancellationToken: cancellationToken));
        return id;
    }

    public async Task CompleteAsync(Guid id, IngestionResult result, DateTimeOffset completedAtUtc, CancellationToken cancellationToken)
    {
        await using var connection = (SqlConnection)connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.IngestionRuns
            SET CompletedAtUtc = @CompletedAtUtc, Status = @Status,
                CompetitionsProcessed = @CompetitionsProcessed, SeasonsProcessed = @SeasonsProcessed,
                TeamsProcessed = @TeamsProcessed, FixturesProcessed = @FixturesProcessed, ErrorMessage = NULL
            WHERE Id = @Id;
            """,
            new { Id = id, CompletedAtUtc = completedAtUtc.UtcDateTime, Status = (byte)IngestionRunStatus.Succeeded,
                result.CompetitionsProcessed, result.SeasonsProcessed, result.TeamsProcessed, result.FixturesProcessed },
            cancellationToken: cancellationToken));
    }

    public async Task FailAsync(Guid id, string errorMessage, DateTimeOffset completedAtUtc, CancellationToken cancellationToken)
    {
        await using var connection = (SqlConnection)connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.IngestionRuns SET CompletedAtUtc = @CompletedAtUtc, Status = @Status, ErrorMessage = @ErrorMessage WHERE Id = @Id;",
            new { Id = id, CompletedAtUtc = completedAtUtc.UtcDateTime, Status = (byte)IngestionRunStatus.Failed, ErrorMessage = errorMessage },
            cancellationToken: cancellationToken));
    }
}
