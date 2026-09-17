using Dapper;
using FootballAnalytics.Domain.Identifiers;
using Microsoft.Data.SqlClient;

namespace FootballAnalytics.Infrastructure.Ingestion;

internal static class ProviderIdentifierSql
{
    public static Task<Guid?> FindAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string providerCode,
        ExternalEntityType entityType,
        string externalId,
        CancellationToken cancellationToken) =>
        connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            SELECT InternalEntityId
            FROM dbo.ProviderIdentifiers
            WHERE Provider = @ProviderCode AND EntityType = @EntityType AND ExternalId = @ExternalId;
            """,
            new { ProviderCode = providerCode, EntityType = (byte)entityType, ExternalId = externalId },
            transaction,
            cancellationToken: cancellationToken));

    public static Task InsertAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string providerCode,
        ExternalEntityType entityType,
        Guid internalEntityId,
        string externalId,
        CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.ProviderIdentifiers (Provider, EntityType, InternalEntityId, ExternalId)
            VALUES (@ProviderCode, @EntityType, @InternalEntityId, @ExternalId);
            """,
            new { ProviderCode = providerCode, EntityType = (byte)entityType, InternalEntityId = internalEntityId, ExternalId = externalId },
            transaction,
            cancellationToken: cancellationToken));
}
