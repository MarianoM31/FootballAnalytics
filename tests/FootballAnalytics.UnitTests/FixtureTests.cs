using FootballAnalytics.Domain.Entities;
using FootballAnalytics.Domain.Enums;

namespace FootballAnalytics.UnitTests;

public sealed class FixtureTests
{
    [Fact]
    public void Fixture_can_represent_a_scheduled_match_without_a_score()
    {
        var fixture = new Fixture
        {
            CompetitionId = Guid.NewGuid(),
            SeasonId = Guid.NewGuid(),
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            KickoffUtc = new DateTimeOffset(2026, 9, 20, 18, 0, 0, TimeSpan.Zero),
            Status = FixtureStatus.Scheduled
        };

        Assert.Equal(FixtureStatus.Scheduled, fixture.Status);
        Assert.Null(fixture.HomeScore);
        Assert.Null(fixture.AwayScore);
    }
}
