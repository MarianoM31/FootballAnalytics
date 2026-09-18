using FootballAnalytics.Domain.Enums;

namespace FootballAnalytics.Application.Historical;

public sealed record NormalizedHistoricalTeamStatistics(
    string TeamExternalId, int? Goals, int? Shots, int? ShotsOnTarget, decimal? PossessionPercentage,
    int? Corners, int? Offsides, int? Fouls, int? YellowCards, int? RedCards);

public sealed record NormalizedHistoricalMatch(
    string CompetitionExternalId, string CompetitionName, string? CompetitionCountry,
    string SeasonExternalId, string SeasonName, DateOnly SeasonStartDate, DateOnly SeasonEndDate,
    string MatchExternalId, string HomeTeamExternalId, string HomeTeamName,
    string AwayTeamExternalId, string AwayTeamName, DateTimeOffset KickoffUtc,
    FixtureStatus Status, int? HomeScore, int? AwayScore,
    IReadOnlyList<NormalizedHistoricalTeamStatistics> TeamStatistics,
    int? EventDerivedHomeGoals, int? EventDerivedAwayGoals);

public interface IHistoricalMatchStatisticsProvider
{
    string ProviderCode { get; }
    Task<IReadOnlyList<NormalizedHistoricalMatch>> GetMatchesAsync(string competitionExternalId, string seasonExternalId, CancellationToken cancellationToken);
}

public sealed record HistoricalIngestionResult(Guid RunId, int CompetitionsProcessed, int SeasonsProcessed, int TeamsProcessed, int FixturesProcessed, int StatisticsSnapshotsCreated);
