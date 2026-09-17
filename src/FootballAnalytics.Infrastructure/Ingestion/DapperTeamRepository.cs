using Dapper;
using FootballAnalytics.Application.Ingestion;
using FootballAnalytics.Application.Persistence;
using FootballAnalytics.Domain.Identifiers;
using Microsoft.Data.SqlClient;

namespace FootballAnalytics.Infrastructure.Ingestion;

public sealed class DapperTeamRepository(IDbConnectionFactory connectionFactory) : ITeamRepository
{
    public async Task<Guid> UpsertAsync(string providerCode, NormalizedTeam team, CancellationToken cancellationToken)
    {
        await using var connection = (SqlConnection)connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var id = await ProviderIdentifierSql.FindAsync(connection, transaction, providerCode, ExternalEntityType.Team, team.ExternalId, cancellationToken);
            if (id.HasValue)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE dbo.Teams SET Name = @Name WHERE Id = @Id;",
                    new { team.Name, Id = id.Value }, transaction, cancellationToken: cancellationToken));
            }
            else
            {
                id = Guid.NewGuid();
                await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO dbo.Teams (Id, Name) VALUES (@Id, @Name);",
                    new { Id = id.Value, team.Name }, transaction, cancellationToken: cancellationToken));
                await ProviderIdentifierSql.InsertAsync(connection, transaction, providerCode, ExternalEntityType.Team, id.Value, team.ExternalId, cancellationToken);
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
