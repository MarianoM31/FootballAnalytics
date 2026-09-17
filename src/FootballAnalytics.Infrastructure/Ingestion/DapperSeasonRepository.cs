using Dapper;
using FootballAnalytics.Application.Ingestion;
using FootballAnalytics.Application.Persistence;
using FootballAnalytics.Domain.Identifiers;
using Microsoft.Data.SqlClient;

namespace FootballAnalytics.Infrastructure.Ingestion;

public sealed class DapperSeasonRepository(IDbConnectionFactory connectionFactory) : ISeasonRepository
{
    public async Task<Guid> UpsertAsync(string providerCode, NormalizedSeason season, Guid competitionId, CancellationToken cancellationToken)
    {
        await using var connection = (SqlConnection)connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var id = await ProviderIdentifierSql.FindAsync(connection, transaction, providerCode, ExternalEntityType.Season, season.ExternalId, cancellationToken);
            if (id.HasValue)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE dbo.Seasons SET CompetitionId = @CompetitionId, Name = @Name, StartDate = @StartDate, EndDate = @EndDate WHERE Id = @Id;",
                    new
                    {
                        Id = id.Value,
                        competitionId,
                        season.Name,
                        StartDate = season.StartDate.ToDateTime(TimeOnly.MinValue),
                        EndDate = season.EndDate.ToDateTime(TimeOnly.MinValue)
                    }, transaction, cancellationToken: cancellationToken));
            }
            else
            {
                id = Guid.NewGuid();
                await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO dbo.Seasons (Id, CompetitionId, Name, StartDate, EndDate) VALUES (@Id, @CompetitionId, @Name, @StartDate, @EndDate);",
                    new
                    {
                        Id = id.Value,
                        competitionId,
                        season.Name,
                        StartDate = season.StartDate.ToDateTime(TimeOnly.MinValue),
                        EndDate = season.EndDate.ToDateTime(TimeOnly.MinValue)
                    }, transaction, cancellationToken: cancellationToken));
                await ProviderIdentifierSql.InsertAsync(connection, transaction, providerCode, ExternalEntityType.Season, id.Value, season.ExternalId, cancellationToken);
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
