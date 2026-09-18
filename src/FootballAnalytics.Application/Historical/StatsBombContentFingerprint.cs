using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FootballAnalytics.Application.Historical;

public static class StatsBombContentFingerprint
{
    public const string PolicyVersion = "statsbomb-open-team-statistics-v1";

    public static string For(NormalizedHistoricalMatch match)
    {
        var teams = match.TeamStatistics.OrderBy(x => x.TeamExternalId, StringComparer.Ordinal)
            .Select(x => string.Join(',', x.TeamExternalId, x.Goals, x.Shots, x.ShotsOnTarget,
                x.PossessionPercentage?.ToString(CultureInfo.InvariantCulture), x.Corners, x.Offsides,
                x.Fouls, x.YellowCards, x.RedCards));
        var text = string.Join('|', [PolicyVersion, "statsbomb-open", match.MatchExternalId,
            match.HomeTeamExternalId, match.AwayTeamExternalId, match.HomeScore, match.AwayScore, .. teams]);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
