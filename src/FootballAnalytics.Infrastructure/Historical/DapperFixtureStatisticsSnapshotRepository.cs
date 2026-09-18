using Dapper;
using FootballAnalytics.Application.Historical;
using FootballAnalytics.Application.Persistence;
using FootballAnalytics.Domain.Entities;
using FootballAnalytics.Domain.Temporal;
using FootballAnalytics.Domain.Validation;
using Microsoft.Data.SqlClient;

namespace FootballAnalytics.Infrastructure.Historical;

public sealed class DapperFixtureStatisticsSnapshotRepository(IDbConnectionFactory connectionFactory) : IFixtureStatisticsSnapshotRepository
{
    public async Task<bool> AddIfAbsentAsync(FixtureStatisticsSnapshot snapshot, IReadOnlyCollection<TeamMatchStatistics> teamStatistics, CancellationToken cancellationToken)
    {
        Validate(snapshot, teamStatistics);
        await using var connection = (SqlConnection)connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var statistics in teamStatistics)
            {
                var participates = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    "SELECT COUNT(*) FROM dbo.Fixtures WHERE Id = @FixtureId AND (HomeTeamId = @TeamId OR AwayTeamId = @TeamId);",
                    new { snapshot.FixtureId, statistics.TeamId }, transaction, cancellationToken: cancellationToken));
                if (participates != 1)
                    throw new InvalidOperationException("Team match statistics must belong to a team participating in the fixture.");
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO dbo.FixtureStatisticsSnapshots
                    (Id, FixtureId, Provider, ObservedAtUtc, AvailableAtUtc, IngestedAtUtc, IngestionRunId, SourceFingerprint)
                VALUES
                    (@Id, @FixtureId, @Provider, @ObservedAtUtc, @AvailableAtUtc, @IngestedAtUtc, @IngestionRunId, @SourceFingerprint);
                """,
                new
                {
                    snapshot.Id, snapshot.FixtureId, snapshot.Provider,
                    ObservedAtUtc = snapshot.ObservedAtUtc?.UtcDateTime,
                    AvailableAtUtc = snapshot.AvailableAtUtc.UtcDateTime,
                    IngestedAtUtc = snapshot.IngestedAtUtc.UtcDateTime,
                    snapshot.IngestionRunId, snapshot.SourceFingerprint
                }, transaction, cancellationToken: cancellationToken));

            foreach (var statistics in teamStatistics)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO dbo.TeamMatchStatistics
                        (Id, FixtureStatisticsSnapshotId, TeamId, Goals, Shots, ShotsOnTarget, PossessionPercentage, Corners, Offsides, Fouls, YellowCards, RedCards)
                    VALUES
                        (@Id, @FixtureStatisticsSnapshotId, @TeamId, @Goals, @Shots, @ShotsOnTarget, @PossessionPercentage, @Corners, @Offsides, @Fouls, @YellowCards, @RedCards);
                    """,
                    statistics, transaction, cancellationToken: cancellationToken));
            }

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

    public async Task<FixtureStatisticsSnapshot?> GetAsOfAsync(Guid fixtureId, string provider, DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
    {
        TemporalObservation.Validate(null, cutoffUtc, cutoffUtc);
        await using var connection = (SqlConnection)connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<SnapshotRow>(new CommandDefinition(
            """
            SELECT TOP (1) Id, FixtureId, Provider, ObservedAtUtc, AvailableAtUtc, IngestedAtUtc, IngestionRunId, SourceFingerprint
            FROM dbo.FixtureStatisticsSnapshots
            WHERE FixtureId = @FixtureId AND Provider = @Provider AND AvailableAtUtc <= @CutoffUtc AND IngestedAtUtc <= @CutoffUtc
            ORDER BY AvailableAtUtc DESC, IngestedAtUtc DESC, Id DESC;
            """,
            new { FixtureId = fixtureId, Provider = provider, CutoffUtc = cutoffUtc.UtcDateTime }, cancellationToken: cancellationToken));
        return row is null ? null : row.ToEntity();
    }

    private static void Validate(FixtureStatisticsSnapshot snapshot, IReadOnlyCollection<TeamMatchStatistics> teamStatistics)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(teamStatistics);
        TemporalObservation.Validate(snapshot.ObservedAtUtc, snapshot.AvailableAtUtc, snapshot.IngestedAtUtc);
        if (string.IsNullOrWhiteSpace(snapshot.Provider)) throw new ArgumentException("Provider is required.", nameof(snapshot));
        if (snapshot.SourceFingerprint.Length != 64) throw new ArgumentException("SourceFingerprint must be a SHA-256 hexadecimal value.", nameof(snapshot));
        if (teamStatistics.Count == 0) throw new ArgumentException("At least one team statistic is required.", nameof(teamStatistics));
        if (teamStatistics.Select(statistics => statistics.TeamId).Distinct().Count() != teamStatistics.Count)
            throw new ArgumentException("A snapshot may contain only one statistic row per team.", nameof(teamStatistics));

        foreach (var statistics in teamStatistics)
        {
            if (statistics.FixtureStatisticsSnapshotId != snapshot.Id)
                throw new ArgumentException("Team statistics must reference the supplied snapshot.", nameof(teamStatistics));
            TeamMatchStatisticsValidator.Validate(statistics);
        }
    }

    private sealed class SnapshotRow
    {
        public Guid Id { get; init; }
        public Guid FixtureId { get; init; }
        public string Provider { get; init; } = string.Empty;
        public DateTime? ObservedAtUtc { get; init; }
        public DateTime AvailableAtUtc { get; init; }
        public DateTime IngestedAtUtc { get; init; }
        public Guid IngestionRunId { get; init; }
        public string SourceFingerprint { get; init; } = string.Empty;

        public FixtureStatisticsSnapshot ToEntity() => new(
            Id, FixtureId, Provider,
            ObservedAtUtc is null ? null : new DateTimeOffset(DateTime.SpecifyKind(ObservedAtUtc.Value, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(AvailableAtUtc, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(IngestedAtUtc, DateTimeKind.Utc)),
            IngestionRunId, SourceFingerprint);
    }
}
