using FootballAnalytics.Domain.Enums;

namespace FootballAnalytics.Domain.Entities;

public sealed class Fixture
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CompetitionId { get; init; }
    public Guid SeasonId { get; init; }
    public Guid HomeTeamId { get; init; }
    public Guid AwayTeamId { get; init; }
    public DateTimeOffset KickoffUtc { get; init; }
    public FixtureStatus Status { get; init; }
    public int? HomeScore { get; init; }
    public int? AwayScore { get; init; }
}
