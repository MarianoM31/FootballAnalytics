using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FootballAnalytics.Application.Features;

public sealed class TeamFeatureService(ITemporalFeatureDataRepository data)
{
    public const string FeaturePolicyVersion = "team-features-v1";
    public async Task<TeamFeatureResult> CalculateAsync(FeatureRequest request, CancellationToken cancellationToken = default)
    {
        if (request.DataCutoffUtc.Offset != TimeSpan.Zero) throw new ArgumentException("DataCutoffUtc must be UTC.");
        var target = await data.GetTargetFixtureAsync(request.TargetFixtureId, request.TeamId, cancellationToken) ?? throw new InvalidOperationException("Target fixture was not found.");
        var scheduleId = await data.GetScheduleObservationIdAsOfAsync(target.FixtureId, request.DataCutoffUtc, cancellationToken);
        var candidates = await data.GetTeamFixturesAsync(request.TeamId, target.CompetitionId, target.SeasonId, request.DataCutoffUtc, request.TemporalContext, cancellationToken);
        var policy = request.CompletionPolicy ?? HistoricalCompletionPolicy.V1;
        var eligible = candidates.Where(x => x.FixtureId != target.FixtureId && x.KickoffUtc < request.DataCutoffUtc && (request.TemporalContext != TemporalContext.HistoricalResearch || x.Status == 2 && x.KickoffUtc + policy.GracePeriod <= request.DataCutoffUtc)).OrderByDescending(x => x.KickoffUtc).ToList();
        var requested = request.Window == FeatureWindow.Last5 ? 5 : (int?)null;
        if (requested.HasValue) eligible = eligible.Take(requested.Value).ToList();
        eligible.Reverse();
        var samples = new MetricSampleSizes(Count(eligible, x => x.Goals), Count(eligible, x => x.Shots), Count(eligible, x => x.ShotsOnTarget), Count(eligible, x => x.Corners), Count(eligible, x => x.Fouls), Count(eligible, x => x.YellowCards), Count(eligible, x => x.RedCards));
        var knownResults = eligible.Where(x => x.HomeScore.HasValue && x.AwayScore.HasValue).ToList();
        var wins = knownResults.Count(x => ScoreFor(x, request.TeamId) > ScoreAgainst(x, request.TeamId)); var draws = knownResults.Count(x => ScoreFor(x, request.TeamId) == ScoreAgainst(x, request.TeamId)); var losses = knownResults.Count - wins - draws;
        var shotsCompatible = eligible.Where(x => x.Shots.HasValue && x.ShotsOnTarget.HasValue).ToList(); var totalShots = shotsCompatible.Sum(x => x.Shots!.Value); var totalOnTarget = shotsCompatible.Sum(x => x.ShotsOnTarget!.Value);
        var result = new TeamFeatureResult(request.TeamId, target.FixtureId, request.DataCutoffUtc, request.TemporalContext, request.Window, eligible.Count, requested, !requested.HasValue || eligible.Count >= requested, FeaturePolicyVersion + ":" + policy.Version, samples, wins, draws, losses, Rate(knownResults.Sum(x => ScoreFor(x, request.TeamId) > ScoreAgainst(x, request.TeamId) ? 3 : ScoreFor(x, request.TeamId) == ScoreAgainst(x, request.TeamId) ? 1 : 0), knownResults.Count), Rate(eligible.Sum(x => x.Goals), samples.Goals), Rate(knownResults.Sum(x => ScoreAgainst(x, request.TeamId)), knownResults.Count), Rate(knownResults.Sum(x => ScoreFor(x, request.TeamId) - ScoreAgainst(x, request.TeamId)), knownResults.Count), Rate(knownResults.Count(x => ScoreAgainst(x, request.TeamId) == 0), knownResults.Count), Rate(eligible.Sum(x => x.Shots), samples.Shots), Rate(eligible.Sum(x => x.ShotsOnTarget), samples.ShotsOnTarget), totalShots == 0 ? null : Rate(totalOnTarget, totalShots), Rate(eligible.Sum(x => x.Corners), samples.Corners), Rate(eligible.Sum(x => x.Fouls), samples.Fouls), Rate(eligible.Sum(x => x.YellowCards), samples.YellowCards), Rate(eligible.Sum(x => x.RedCards), samples.RedCards), eligible.Select(x => x.FixtureId).ToArray(), eligible.Select(x => x.SnapshotId).ToArray(), scheduleId, string.Empty);
        return result with { Fingerprint = Fingerprint(result) };
    }
    private static int Count(IEnumerable<FeatureFixture> x, Func<FeatureFixture, int?> value) => x.Count(y => value(y).HasValue);
    private static int ScoreFor(FeatureFixture x, Guid team) => x.HomeTeamId == team ? x.HomeScore!.Value : x.AwayScore!.Value;
    private static int ScoreAgainst(FeatureFixture x, Guid team) => x.HomeTeamId == team ? x.AwayScore!.Value : x.HomeScore!.Value;
    private static decimal? Rate(int? total, int count) => count == 0 || !total.HasValue ? null : decimal.Round((decimal)total.Value / count, 4);
    private static string Fingerprint(TeamFeatureResult x) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', FeaturePolicyVersion, x.TemporalContext, x.DataCutoffUtc.UtcTicks, x.TeamId, x.TargetFixtureId, x.Window, string.Join(',', x.SourceFixtureIds), string.Join(',', x.SourceSnapshotIds), x.SampleSize, x.PointsPerMatch?.ToString(CultureInfo.InvariantCulture), x.ShotsPerMatch?.ToString(CultureInfo.InvariantCulture), x.MetricSampleSizes))));
}
