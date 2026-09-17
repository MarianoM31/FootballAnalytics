using FootballAnalytics.Domain.Enums;

namespace FootballAnalytics.Application.Ingestion;

public sealed record NormalizedCompetition(string ExternalId, string Name, string? Country);

public sealed record NormalizedSeason(
    string ExternalId,
    string CompetitionExternalId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record NormalizedTeam(string ExternalId, string Name);

public sealed record NormalizedFixture(
    string ExternalId,
    string CompetitionExternalId,
    string SeasonExternalId,
    string HomeTeamExternalId,
    string AwayTeamExternalId,
    DateTimeOffset KickoffUtc,
    FixtureStatus Status,
    int? HomeScore,
    int? AwayScore);
