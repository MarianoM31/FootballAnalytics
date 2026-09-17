using Dapper;
using FootballAnalytics.Application.Ingestion;
using FootballAnalytics.Application.Persistence;
using FootballAnalytics.Domain.Identifiers;
using Microsoft.Data.SqlClient;

namespace FootballAnalytics.Infrastructure.Ingestion;

public sealed class DapperCompetitionRepository(IDbConnectionFactory connectionFactory) : ICompetitionRepository
{
    public async Task<Guid> UpsertAsync(string providerCode, NormalizedCompetition competition, CancellationToken cancellationToken)
    {
        await using var connection = (SqlConnection)connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var id = await ProviderIdentifierSql.FindAsync(connection, transaction, providerCode, ExternalEntityType.Competition, competition.ExternalId, cancellationToken);
            if (id.HasValue)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE dbo.Competitions SET Name = @Name, Country = @Country WHERE Id = @Id;",
                    new { competition.Name, competition.Country, Id = id.Value }, transaction, cancellationToken: cancellationToken));
            }
            else
            {
                id = Guid.NewGuid();
                await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO dbo.Competitions (Id, Name, Country) VALUES (@Id, @Name, @Country);",
                    new { Id = id.Value, competition.Name, competition.Country }, transaction, cancellationToken: cancellationToken));
                await ProviderIdentifierSql.InsertAsync(connection, transaction, providerCode, ExternalEntityType.Competition, id.Value, competition.ExternalId, cancellationToken);
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
