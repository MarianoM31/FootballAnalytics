using FootballAnalytics.Application.Features;

namespace FootballAnalytics.UnitTests;

public sealed class TeamFeatureServiceTests
{
    [Fact]
    public async Task Historical_research_enforces_grace_boundary_and_excludes_target_and_future()
    {
        var now = new DateTimeOffset(2020, 1, 2, 12, 0, 0, TimeSpan.Zero); var target = Fixture(10, now);
        var eligible = Fixture(1, now.AddHours(-3)); var tooRecent = Fixture(2, now.AddHours(-2)); var future = Fixture(3, now.AddHours(1));
        var result = await Service(target, [eligible, tooRecent, target, future]).CalculateAsync(new(target.FixtureId, Team, now, TemporalContext.HistoricalResearch, FeatureWindow.Last5));
        Assert.Equal(1, result.SampleSize); Assert.Equal([eligible.FixtureId], result.SourceFixtureIds);
    }

    [Fact]
    public async Task Null_zero_and_compatible_shot_accuracy_use_correct_denominators()
    {
        var cutoff = new DateTimeOffset(2020, 1, 5, 12, 0, 0, TimeSpan.Zero); var target = Fixture(10, cutoff);
        var a = Fixture(1, cutoff.AddDays(-4), shots: 10, onTarget: 4); var b = Fixture(2, cutoff.AddDays(-3), shots: null, onTarget: 3); var c = Fixture(3, cutoff.AddDays(-2), shots: 5, onTarget: 2);
        var result = await Service(target, [a,b,c]).CalculateAsync(new(target.FixtureId, Team, cutoff, TemporalContext.HistoricalResearch, FeatureWindow.Last5));
        Assert.Equal(3, result.SampleSize); Assert.Equal(2, result.MetricSampleSizes.Shots); Assert.Equal(7.5m, result.ShotsPerMatch); Assert.Equal(0.4m, result.ShotAccuracy);
        var zeros = await Service(target, [Fixture(4, cutoff.AddDays(-4), shots:10), Fixture(5, cutoff.AddDays(-3), shots:0), Fixture(6, cutoff.AddDays(-2), shots:8)]).CalculateAsync(new(target.FixtureId, Team, cutoff, TemporalContext.HistoricalResearch, FeatureWindow.Last5));
        Assert.Equal(3, zeros.MetricSampleSizes.Shots); Assert.Equal(6m, zeros.ShotsPerMatch);
    }

    [Fact]
    public async Task Last5_is_deterministic_and_reports_small_sample()
    {
        var cutoff = new DateTimeOffset(2020, 1, 5, 12, 0, 0, TimeSpan.Zero); var target = Fixture(10, cutoff); var source = new[]{Fixture(1,cutoff.AddDays(-4)),Fixture(2,cutoff.AddDays(-3)),Fixture(3,cutoff.AddDays(-2))};
        var service=Service(target, source); var request=new FeatureRequest(target.FixtureId,Team,cutoff,TemporalContext.HistoricalResearch,FeatureWindow.Last5);
        var first=await service.CalculateAsync(request); var second=await service.CalculateAsync(request);
        Assert.Equal(3, first.SampleSize); Assert.Equal(5, first.RequestedSampleSize); Assert.False(first.IsCompleteWindow); Assert.Equal(first.Fingerprint,second.Fingerprint); Assert.Equal(first.SourceFixtureIds,second.SourceFixtureIds); Assert.Equal(first.SourceSnapshotIds,second.SourceSnapshotIds);
    }

    [Fact]
    public async Task Known_zero_shots_has_null_accuracy()
    {
        var cutoff=new DateTimeOffset(2020,1,5,12,0,0,TimeSpan.Zero); var target=Fixture(10,cutoff); var result=await Service(target,[Fixture(1,cutoff.AddDays(-1),shots:0,onTarget:0)]).CalculateAsync(new(target.FixtureId,Team,cutoff,TemporalContext.HistoricalResearch,FeatureWindow.Last5));
        Assert.Null(result.ShotAccuracy);
    }

    private static readonly Guid Team=Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static FeatureFixture Fixture(int id, DateTimeOffset kickoff, int? shots=10, int? onTarget=4) => new(Guid.Parse($"00000000-0000-0000-0000-{id:D12}"),Guid.NewGuid(),Guid.NewGuid(),Team,Guid.NewGuid(),kickoff,(byte)2,2,1,Guid.NewGuid(),kickoff,kickoff,Guid.NewGuid(),2,shots,onTarget,1,1,0,0);
    private static TeamFeatureService Service(FeatureFixture target, IEnumerable<FeatureFixture> data) => new(new Fake(target,data.ToArray()));
    private sealed class Fake(FeatureFixture target, FeatureFixture[] items) : ITemporalFeatureDataRepository
    { public Task<FeatureFixture?> GetTargetFixtureAsync(Guid id,Guid team,CancellationToken ct)=>Task.FromResult<FeatureFixture?>(target); public Task<Guid?> GetScheduleObservationIdAsOfAsync(Guid id,DateTimeOffset cut,CancellationToken ct)=>Task.FromResult<Guid?>(null); public Task<IReadOnlyList<FeatureFixture>> GetTeamFixturesAsync(Guid team,Guid comp,Guid season,DateTimeOffset cut,TemporalContext context,CancellationToken ct)=>Task.FromResult<IReadOnlyList<FeatureFixture>>(items); }
}
