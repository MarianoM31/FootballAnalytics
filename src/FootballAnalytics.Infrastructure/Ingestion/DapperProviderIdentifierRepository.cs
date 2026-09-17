using Dapper;
using FootballAnalytics.Application.Ingestion;
using FootballAnalytics.Application.Persistence;
using FootballAnalytics.Domain.Identifiers;
using Microsoft.Data.SqlClient;

namespace FootballAnalytics.Infrastructure.Ingestion;

public sealed class DapperProviderIdentifierRepository(IDbConnectionFactory connectionFactory) : IProviderIdentifierRepository
{
    public async Task<Guid?> FindInternalEntityIdAsync(
        string providerCode,
        ExternalEntityType entityType,
        string externalId,
        CancellationToken cancellationToken)
    {
        await using var connection = (SqlConnection)connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await ProviderIdentifierSql.FindAsync(connection, null, providerCode, entityType, externalId, cancellationToken);
    }
}
