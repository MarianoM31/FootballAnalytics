using Dapper;
using FootballAnalytics.Application.Historical;
using FootballAnalytics.Application.Persistence;
using FootballAnalytics.Domain.Entities;
using FootballAnalytics.Domain.Temporal;
using Microsoft.Data.SqlClient;

namespace FootballAnalytics.Infrastructure.Historical;

public sealed class DapperFixtureScheduleObservationRepository(IDbConnectionFactory connectionFactory) : IFixtureScheduleObservationRepository
{
    public async Task<bool> AddIfAbsentAsync(FixtureScheduleObservation observation, CancellationToken cancellationToken)
    {
        Validate(observation);
        await using var connection = (SqlConnection)connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO dbo.FixtureScheduleObservations
                    (Id, FixtureId, Provider, KickoffUtc, Status, ObservedAtUtc, AvailableAtUtc, IngestedAtUtc, IngestionRunId, SourceFingerprint)
                VALUES
                    (@Id, @FixtureId, @Provider, @KickoffUtc, @Status, @ObservedAtUtc, @AvailableAtUtc, @IngestedAtUtc, @IngestionRunId, @SourceFingerprint);
                """,
                new
                {
                    observation.Id,
                    observation.FixtureId,
                    observation.Provider,
                    KickoffUtc = observation.KickoffUtc.UtcDateTime,
                    Status = (byte)observation.Status,
                    ObservedAtUtc = observation.ObservedAtUtc?.UtcDateTime,
                    AvailableAtUtc = observation.AvailableAtUtc.UtcDateTime,
                    IngestedAtUtc = observation.IngestedAtUtc.UtcDateTime,
                    observation.IngestionRunId,
                    observation.SourceFingerprint
                }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return false;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static void Validate(FixtureScheduleObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        TemporalObservation.Validate(observation.ObservedAtUtc, observation.AvailableAtUtc, observation.IngestedAtUtc);
        if (observation.KickoffUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("KickoffUtc must be expressed in UTC.", nameof(observation));
        if (string.IsNullOrWhiteSpace(observation.Provider))
            throw new ArgumentException("Provider is required.", nameof(observation));
        if (observation.SourceFingerprint.Length != 64)
            throw new ArgumentException("SourceFingerprint must be a SHA-256 hexadecimal value.", nameof(observation));
    }
}
