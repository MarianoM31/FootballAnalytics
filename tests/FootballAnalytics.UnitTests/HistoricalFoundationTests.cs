using FootballAnalytics.Application.Historical;
using FootballAnalytics.Domain.Entities;
using FootballAnalytics.Domain.Enums;
using FootballAnalytics.Domain.Temporal;
using FootballAnalytics.Domain.Validation;

namespace FootballAnalytics.UnitTests;

public sealed class HistoricalFoundationTests
{
    private static readonly DateTimeOffset IngestedAtUtc = new(2026, 9, 20, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Unknown_provider_availability_uses_ingestion_time_conservatively() =>
        Assert.Equal(IngestedAtUtc, TemporalObservation.ResolveAvailableAtUtc(null, IngestedAtUtc));

    [Fact]
    public void As_of_filter_rejects_observations_available_after_the_cutoff()
    {
        var cutoff = IngestedAtUtc.AddMinutes(-1);

        Assert.False(TemporalObservation.IsAvailableAsOf(IngestedAtUtc, IngestedAtUtc, cutoff));
        Assert.True(TemporalObservation.IsAvailableAsOf(IngestedAtUtc, IngestedAtUtc, IngestedAtUtc));
    }

    [Fact]
    public void Temporal_validation_rejects_availability_after_ingestion() =>
        Assert.Throws<ArgumentException>(() => TemporalObservation.Validate(null, IngestedAtUtc.AddMinutes(1), IngestedAtUtc));

    [Fact]
    public void Team_statistics_allow_unknown_values_but_reject_invalid_values()
    {
        var nullableStatistics = CreateTeamStatistics(goals: null, possessionPercentage: null);
        TeamMatchStatisticsValidator.Validate(nullableStatistics);

        Assert.Throws<ArgumentOutOfRangeException>(() => TeamMatchStatisticsValidator.Validate(CreateTeamStatistics(goals: -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => TeamMatchStatisticsValidator.Validate(CreateTeamStatistics(possessionPercentage: 100.01m)));
    }

    [Fact]
    public void Statistics_fingerprint_is_deterministic_and_changes_when_statistics_change()
    {
        var snapshot = CreateSnapshot();
        var first = CreateTeamStatistics(teamId: Guid.Parse("10000000-0000-0000-0000-000000000000"), goals: 1);
        var second = CreateTeamStatistics(teamId: Guid.Parse("20000000-0000-0000-0000-000000000000"), goals: 0);

        var fingerprint = HistoricalObservationFingerprint.ForStatistics(snapshot, [first, second]);

        Assert.Equal(fingerprint, HistoricalObservationFingerprint.ForStatistics(snapshot, [second, first]));
        Assert.NotEqual(fingerprint, HistoricalObservationFingerprint.ForStatistics(snapshot, [first with { Goals = 2 }, second]));
        Assert.Equal(64, fingerprint.Length);
    }

    [Fact]
    public void Schedule_fingerprint_does_not_depend_on_ingestion_run_identity()
    {
        var first = new FixtureScheduleObservation(Guid.NewGuid(), Guid.NewGuid(), "test", IngestedAtUtc, FixtureStatus.Scheduled, null, IngestedAtUtc, IngestedAtUtc, Guid.NewGuid(), string.Empty);
        var second = first with { IngestionRunId = Guid.NewGuid() };

        Assert.Equal(HistoricalObservationFingerprint.ForSchedule(first), HistoricalObservationFingerprint.ForSchedule(second));
    }

    private static FixtureStatisticsSnapshot CreateSnapshot() => new(
        Guid.Parse("00000000-0000-0000-0000-000000000001"),
        Guid.Parse("00000000-0000-0000-0000-000000000002"),
        "test", null, IngestedAtUtc, IngestedAtUtc,
        Guid.Parse("00000000-0000-0000-0000-000000000003"), string.Empty);

    private static TeamMatchStatistics CreateTeamStatistics(Guid? teamId = null, int? goals = 1, decimal? possessionPercentage = 50m) => new(
        Guid.NewGuid(),
        Guid.Parse("00000000-0000-0000-0000-000000000001"),
        teamId ?? Guid.NewGuid(), goals, null, null, possessionPercentage, null, null, null, null, null);
}
