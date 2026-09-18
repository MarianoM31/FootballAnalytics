using FootballAnalytics.Domain.Entities;

namespace FootballAnalytics.Application.Historical;

public interface IFixtureScheduleObservationRepository
{
    Task<bool> AddIfAbsentAsync(FixtureScheduleObservation observation, CancellationToken cancellationToken);
}

public interface IFixtureStatisticsSnapshotRepository
{
    Task<bool> AddIfAbsentAsync(
        FixtureStatisticsSnapshot snapshot,
        IReadOnlyCollection<TeamMatchStatistics> teamStatistics,
        CancellationToken cancellationToken);

    Task<FixtureStatisticsSnapshot?> GetAsOfAsync(
        Guid fixtureId,
        string provider,
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken);
}
