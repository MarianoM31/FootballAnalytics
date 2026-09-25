namespace FootballAnalytics.Application.Features;

public enum TemporalContext { LiveOperational, HistoricalResearch }
public enum FeatureWindow { Last5, SeasonToDate }
public sealed record HistoricalCompletionPolicy(string Version, TimeSpan GracePeriod)
{
    public static HistoricalCompletionPolicy V1 { get; } = new("historical-completion-v1", TimeSpan.FromHours(3));
}
public sealed record FeatureRequest(Guid TargetFixtureId, Guid TeamId, DateTimeOffset DataCutoffUtc, TemporalContext TemporalContext, FeatureWindow Window, HistoricalCompletionPolicy? CompletionPolicy = null);
public sealed record MetricSampleSizes(int Goals, int Shots, int ShotsOnTarget, int Corners, int Fouls, int YellowCards, int RedCards);
public sealed record TeamFeatureResult(Guid TeamId, Guid TargetFixtureId, DateTimeOffset DataCutoffUtc, TemporalContext TemporalContext, FeatureWindow Window, int SampleSize, int? RequestedSampleSize, bool IsCompleteWindow, string FeaturePolicyVersion, MetricSampleSizes MetricSampleSizes, int Wins, int Draws, int Losses, decimal? PointsPerMatch, decimal? GoalsForPerMatch, decimal? GoalsAgainstPerMatch, decimal? GoalDifferencePerMatch, decimal? CleanSheetRate, decimal? ShotsPerMatch, decimal? ShotsOnTargetPerMatch, decimal? ShotAccuracy, decimal? CornersPerMatch, decimal? FoulsPerMatch, decimal? YellowCardsPerMatch, decimal? RedCardsPerMatch, IReadOnlyList<Guid> SourceFixtureIds, IReadOnlyList<Guid> SourceSnapshotIds, Guid? TargetScheduleObservationId, string Fingerprint);
public sealed record FeatureFixture(Guid FixtureId, Guid CompetitionId, Guid SeasonId, Guid HomeTeamId, Guid AwayTeamId, DateTimeOffset KickoffUtc, byte Status, int? HomeScore, int? AwayScore, Guid SnapshotId, DateTimeOffset SnapshotAvailableAtUtc, DateTimeOffset SnapshotIngestedAtUtc, Guid SnapshotIngestionRunId, int? Goals, int? Shots, int? ShotsOnTarget, int? Corners, int? Fouls, int? YellowCards, int? RedCards);
public interface ITemporalFeatureDataRepository
{
    Task<FeatureFixture?> GetTargetFixtureAsync(Guid fixtureId, Guid teamId, CancellationToken cancellationToken);
    Task<Guid?> GetScheduleObservationIdAsOfAsync(Guid fixtureId, DateTimeOffset cutoffUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<FeatureFixture>> GetTeamFixturesAsync(Guid teamId, Guid competitionId, Guid seasonId, DateTimeOffset cutoffUtc, TemporalContext context, CancellationToken cancellationToken);
}
