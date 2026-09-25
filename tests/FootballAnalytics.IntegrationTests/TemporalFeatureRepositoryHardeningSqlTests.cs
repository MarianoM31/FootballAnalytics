using Dapper;
using FootballAnalytics.Application.Features;
using FootballAnalytics.Infrastructure.Features;
using FootballAnalytics.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace FootballAnalytics.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class TemporalFeatureRepositoryHardeningSqlTests : IAsyncLifetime
{
    private readonly string provider = $"phase2c-hardening-{Guid.NewGuid():N}";
    private SqlConnection connection = null!; private SqlConnectionFactory factory = null!;
    private readonly Guid competition=Guid.NewGuid(), otherCompetition=Guid.NewGuid(), season=Guid.NewGuid(), otherSeason=Guid.NewGuid(), team=Guid.NewGuid(), opponent=Guid.NewGuid(), target=Guid.NewGuid(), corrected=Guid.NewGuid();
    private readonly DateTimeOffset t1=new(2020,1,1,12,0,0,TimeSpan.Zero), t2=new(2020,1,2,12,0,0,TimeSpan.Zero), cutoff=new(2020,1,3,12,0,0,TimeSpan.Zero);
    public async Task InitializeAsync()
    {
        var config=new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json").Build(); factory=new(config.GetConnectionString("FootballAnalyticsDb")!); connection=(SqlConnection)factory.CreateConnection(); await connection.OpenAsync();
        await connection.ExecuteAsync("INSERT INTO dbo.Competitions(Id,Name) VALUES(@competition,'x'),(@otherCompetition,'y'); INSERT INTO dbo.Seasons(Id,CompetitionId,Name,StartDate,EndDate) VALUES(@season,@competition,'s','2020-01-01','2020-12-31'),(@otherSeason,@competition,'o','2019-01-01','2019-12-31'); INSERT INTO dbo.Teams(Id,Name) VALUES(@team,'hardening team'),(@opponent,'hardening opponent');",new{competition,otherCompetition,season,otherSeason,team,opponent});
        await AddFixture(target,competition,season,cutoff,2,1,t1,t1,10);
        await AddFixture(corrected,competition,season,cutoff.AddDays(-2),2,1,t1,t1,10);
        await AddSnapshot(corrected,t2,t2,99);
        await AddFixture(Guid.NewGuid(),competition,season,cutoff.AddHours(-3),2,1,t1.AddDays(3),t1.AddDays(3),20); // both timestamps future
        await AddFixture(Guid.NewGuid(),competition,season,cutoff.AddHours(-4),2,1,t1,t1.AddDays(3),30); // ingested future
        await AddFixture(Guid.NewGuid(),competition,season,cutoff.AddHours(-5),2,1,cutoff,cutoff,40); // inclusive boundary
        await AddFixture(Guid.NewGuid(),competition,otherSeason,cutoff.AddDays(-3),2,1,t1,t1,500);
        await AddFixture(Guid.NewGuid(),otherCompetition,season,cutoff.AddDays(-3),2,1,t1,t1,600);
        await AddFixture(Guid.NewGuid(),competition,season,cutoff.AddDays(2),2,999,t1,t1,999); // poison
        var original=Guid.NewGuid(); var later=Guid.NewGuid(); await connection.ExecuteAsync("INSERT INTO dbo.IngestionRuns(Id,Provider,StartedAtUtc,Status,CompetitionsProcessed,SeasonsProcessed,TeamsProcessed,FixturesProcessed) VALUES(@original,@provider,@t1,1,0,0,0,0),(@later,@provider,@t2,1,0,0,0,0); INSERT INTO dbo.FixtureScheduleObservations(Id,FixtureId,Provider,KickoffUtc,Status,AvailableAtUtc,IngestedAtUtc,IngestionRunId,SourceFingerprint) VALUES(@original,@target,@provider,@t1,0,@t1,@t1,@original,REPLICATE('A',64)),(@later,@target,@provider,@t2,0,@t2,@t2,@later,REPLICATE('B',64));",new{original,later,provider,t1,t2,target}); OriginalSchedule=original; LaterSchedule=later;
    }
    public Guid OriginalSchedule{get;private set;} public Guid LaterSchedule{get;private set;}
    public async Task DisposeAsync(){await connection.ExecuteAsync("DELETE ts FROM dbo.TeamMatchStatistics ts JOIN dbo.FixtureStatisticsSnapshots ss ON ss.Id=ts.FixtureStatisticsSnapshotId WHERE ss.IngestionRunId IN (SELECT Id FROM dbo.IngestionRuns WHERE Provider=@provider); DELETE FROM dbo.FixtureScheduleObservations WHERE Provider=@provider; DELETE ss FROM dbo.FixtureStatisticsSnapshots ss JOIN dbo.IngestionRuns r ON r.Id=ss.IngestionRunId WHERE r.Provider=@provider; DELETE FROM dbo.Fixtures WHERE Id IN (SELECT InternalEntityId FROM dbo.ProviderIdentifiers WHERE Provider=@provider) OR HomeTeamId=@team; DELETE FROM dbo.Seasons WHERE Id IN (@season,@otherSeason); DELETE FROM dbo.Competitions WHERE Id IN (@competition,@otherCompetition); DELETE FROM dbo.Teams WHERE Id IN (@team,@opponent); DELETE FROM dbo.IngestionRuns WHERE Provider=@provider;",new{provider,team,season,otherSeason,competition,otherCompetition,opponent}); await connection.DisposeAsync();}
    [Fact] public async Task LiveOperational_selects_original_then_correction_and_honors_boundaries()
    { var repo=new DapperTemporalFeatureDataRepository(factory); var between=await repo.GetTeamFixturesAsync(team,competition,season,t1.AddHours(1),TemporalContext.LiveOperational,CancellationToken.None); Assert.Contains(between,x=>x.FixtureId==corrected&&x.Shots==10); var after=await repo.GetTeamFixturesAsync(team,competition,season,t2.AddHours(1),TemporalContext.LiveOperational,CancellationToken.None); Assert.Contains(after,x=>x.FixtureId==corrected&&x.Shots==99); Assert.DoesNotContain(after,x=>x.Shots==20||x.Shots==30); var boundary=await repo.GetTeamFixturesAsync(team,competition,season,cutoff,TemporalContext.LiveOperational,CancellationToken.None); Assert.Contains(boundary,x=>x.Shots==40); }
    [Fact] public async Task Schedule_is_resolved_as_of_cutoff()
    { var repo=new DapperTemporalFeatureDataRepository(factory); Assert.Equal(OriginalSchedule,await repo.GetScheduleObservationIdAsOfAsync(target,t1.AddHours(1),CancellationToken.None)); Assert.Equal(LaterSchedule,await repo.GetScheduleObservationIdAsOfAsync(target,t2,CancellationToken.None)); }
    [Fact] public async Task HistoricalResearch_is_deterministic_and_excludes_target_and_future()
    { var svc=new TeamFeatureService(new DapperTemporalFeatureDataRepository(factory)); var request=new FeatureRequest(target,team,cutoff,TemporalContext.HistoricalResearch,FeatureWindow.Last5); var a=await svc.CalculateAsync(request); var b=await svc.CalculateAsync(request); Assert.Equal(a.Fingerprint,b.Fingerprint); Assert.DoesNotContain(target,a.SourceFixtureIds); Assert.DoesNotContain(Guid.Empty,a.SourceSnapshotIds); }
    private async Task AddFixture(Guid id,Guid comp,Guid sea,DateTimeOffset kickoff,int score,int away,DateTimeOffset available,DateTimeOffset ingested,int shots){await connection.ExecuteAsync("INSERT INTO dbo.Fixtures(Id,CompetitionId,SeasonId,HomeTeamId,AwayTeamId,KickoffUtc,Status,HomeScore,AwayScore) VALUES(@id,@comp,@sea,@team,@opponent,@kickoff,2,@score,@away); INSERT INTO dbo.ProviderIdentifiers(Provider,EntityType,InternalEntityId,ExternalId) VALUES(@provider,4,@id,CONVERT(varchar(36),@id));",new{id,comp,sea,team,opponent,kickoff,score,away,provider}); await AddSnapshot(id,available,ingested,shots);}
    private async Task AddSnapshot(Guid fixture,DateTimeOffset available,DateTimeOffset ingested,int shots){var run=Guid.NewGuid();var snap=Guid.NewGuid();await connection.ExecuteAsync("INSERT INTO dbo.IngestionRuns(Id,Provider,StartedAtUtc,Status,CompetitionsProcessed,SeasonsProcessed,TeamsProcessed,FixturesProcessed) VALUES(@run,@provider,@ingested,1,0,0,0,0); INSERT INTO dbo.FixtureStatisticsSnapshots(Id,FixtureId,Provider,AvailableAtUtc,IngestedAtUtc,IngestionRunId,SourceFingerprint) VALUES(@snap,@fixture,@provider,@available,@ingested,@run,CONVERT(varchar(64),@snap)); INSERT INTO dbo.TeamMatchStatistics(Id,FixtureStatisticsSnapshotId,TeamId,Goals,Shots,ShotsOnTarget,Corners,Fouls,YellowCards,RedCards) VALUES(NEWID(),@snap,@team,2,@shots,1,1,1,0,0);",new{run,snap,fixture,provider,available,ingested,team,shots});}
}
