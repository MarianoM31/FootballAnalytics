using Dapper;
using FootballAnalytics.Application.Ingestion;
using FootballAnalytics.Application.Persistence;
using FootballAnalytics.Domain.Identifiers;
using Microsoft.Data.SqlClient;

namespace FootballAnalytics.Infrastructure.Ingestion;

public sealed class DapperFixtureRepository(IDbConnectionFactory connectionFactory) : IFixtureRepository
{
    public async Task<Guid> UpsertAsync(
        string providerCode,
        NormalizedFixture fixture,
        Guid competitionId,
        Guid seasonId,
        Guid homeTeamId,
        Guid awayTeamId,
        CancellationToken cancellationToken)
    {
        await using var connection = (SqlConnection)connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var id = await ProviderIdentifierSql.FindAsync(connection, transaction, providerCode, ExternalEntityType.Fixture, fixture.ExternalId, cancellationToken);
            var parameters = new
            {
                Id = id ?? Guid.NewGuid(),
                competitionId,
                seasonId,
                homeTeamId,
                awayTeamId,
                KickoffUtc = fixture.KickoffUtc.UtcDateTime,
                Status = (byte)fixture.Status,
                fixture.HomeScore,
                fixture.AwayScore
            };

            if (id.HasValue)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE dbo.Fixtures
                    SET CompetitionId = @competitionId, SeasonId = @seasonId, HomeTeamId = @homeTeamId, AwayTeamId = @awayTeamId,
                        KickoffUtc = @KickoffUtc, Status = @Status, HomeScore = @HomeScore, AwayScore = @AwayScore
                    WHERE Id = @Id;
                    """,
                    parameters, transaction, cancellationToken: cancellationToken));
            }
            else
            {
                id = parameters.Id;
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO dbo.Fixtures
                        (Id, CompetitionId, SeasonId, HomeTeamId, AwayTeamId, KickoffUtc, Status, HomeScore, AwayScore)
                    VALUES
                        (@Id, @competitionId, @seasonId, @homeTeamId, @awayTeamId, @KickoffUtc, @Status, @HomeScore, @AwayScore);
                    """,
                    parameters, transaction, cancellationToken: cancellationToken));
                await ProviderIdentifierSql.InsertAsync(connection, transaction, providerCode, ExternalEntityType.Fixture, id.Value, fixture.ExternalId, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return id.Value;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
