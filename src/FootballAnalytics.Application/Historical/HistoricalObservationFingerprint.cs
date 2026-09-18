using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FootballAnalytics.Domain.Entities;

namespace FootballAnalytics.Application.Historical;

public static class HistoricalObservationFingerprint
{
    public static string ForSchedule(FixtureScheduleObservation observation) => Hash(string.Join('|',
        observation.FixtureId,
        observation.Provider,
        observation.KickoffUtc.UtcDateTime.Ticks,
        (byte)observation.Status,
        observation.ObservedAtUtc?.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        observation.AvailableAtUtc.UtcDateTime.Ticks));

    public static string ForStatistics(FixtureStatisticsSnapshot snapshot, IEnumerable<TeamMatchStatistics> teamStatistics)
    {
        var values = teamStatistics.OrderBy(statistics => statistics.TeamId)
            .Select(statistics => string.Join(',', statistics.TeamId, statistics.Goals, statistics.Shots, statistics.ShotsOnTarget,
                statistics.PossessionPercentage?.ToString(CultureInfo.InvariantCulture), statistics.Corners, statistics.Offsides,
                statistics.Fouls, statistics.YellowCards, statistics.RedCards));
        return Hash(string.Join('|', [
            snapshot.FixtureId.ToString(), snapshot.Provider,
            snapshot.ObservedAtUtc?.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            snapshot.AvailableAtUtc.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture),
            .. values]));
    }

    private static string Hash(string input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}
