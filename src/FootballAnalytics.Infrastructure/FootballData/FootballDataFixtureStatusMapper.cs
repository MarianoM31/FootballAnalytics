using FootballAnalytics.Domain.Enums;

namespace FootballAnalytics.Infrastructure.FootballData;

public static class FootballDataFixtureStatusMapper
{
    public static FixtureStatus Map(string? externalStatus) => externalStatus switch
    {
        "SCHEDULED" or "TIMED" => FixtureStatus.Scheduled,
        "IN_PLAY" or "PAUSED" or "EXTRA_TIME" or "PENALTY_SHOOTOUT" => FixtureStatus.InProgress,
        "FINISHED" or "AWARDED" => FixtureStatus.Finished,
        "POSTPONED" or "SUSPENDED" => FixtureStatus.Postponed,
        "CANCELLED" => FixtureStatus.Cancelled,
        _ => throw new FootballDataProviderException($"football-data.org returned an unsupported fixture status '{externalStatus ?? "null"}'.")
    };
}
