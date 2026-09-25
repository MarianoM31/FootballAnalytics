using Dapper;
using FootballAnalytics.Application.Features;
using FootballAnalytics.Application.Persistence;
using Microsoft.Data.SqlClient;

namespace FootballAnalytics.Infrastructure.Features;

public sealed class DapperTemporalFeatureDataRepository(IDbConnectionFactory connections) : ITemporalFeatureDataRepository
{
    public async Task<FeatureFixture?> GetTargetFixtureAsync(Guid fixtureId, Guid teamId, CancellationToken cancellationToken)
    {
        await using var c = (SqlConnection)connections.CreateConnection(); await c.OpenAsync(cancellationToken);
        const string sql = "SELECT f.Id FixtureId,f.CompetitionId,f.SeasonId,f.HomeTeamId,f.AwayTeamId,f.KickoffUtc,f.Status,f.HomeScore,f.AwayScore,CAST('00000000-0000-0000-0000-000000000000' AS uniqueidentifier) SnapshotId,f.KickoffUtc SnapshotAvailableAtUtc,f.KickoffUtc SnapshotIngestedAtUtc,CAST('00000000-0000-0000-0000-000000000000' AS uniqueidentifier) SnapshotIngestionRunId,CAST(NULL AS int) Goals,CAST(NULL AS int) Shots,CAST(NULL AS int) ShotsOnTarget,CAST(NULL AS int) Corners,CAST(NULL AS int) Fouls,CAST(NULL AS int) YellowCards,CAST(NULL AS int) RedCards FROM dbo.Fixtures f WHERE f.Id=@FixtureId AND (f.HomeTeamId=@TeamId OR f.AwayTeamId=@TeamId)";
        var row = await c.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(sql, new { FixtureId = fixtureId, TeamId = teamId }, cancellationToken: cancellationToken)); return row?.ToFeature();
    }
    public async Task<Guid?> GetScheduleObservationIdAsOfAsync(Guid fixtureId, DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
    {
        await using var c = (SqlConnection)connections.CreateConnection(); await c.OpenAsync(cancellationToken);
        return await c.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("SELECT TOP(1) Id FROM dbo.FixtureScheduleObservations WHERE FixtureId=@FixtureId AND AvailableAtUtc<=@CutoffUtc AND IngestedAtUtc<=@CutoffUtc ORDER BY AvailableAtUtc DESC, IngestedAtUtc DESC, Id DESC", new { FixtureId = fixtureId, CutoffUtc = cutoffUtc.UtcDateTime }, cancellationToken: cancellationToken));
    }
    public async Task<IReadOnlyList<FeatureFixture>> GetTeamFixturesAsync(Guid teamId, Guid competitionId, Guid seasonId, DateTimeOffset cutoffUtc, TemporalContext context, CancellationToken cancellationToken)
    {
        await using var c = (SqlConnection)connections.CreateConnection(); await c.OpenAsync(cancellationToken);
        var sql = Select + " WHERE (f.HomeTeamId=@TeamId OR f.AwayTeamId=@TeamId) AND f.CompetitionId=@CompetitionId AND f.SeasonId=@SeasonId AND f.KickoffUtc<@CutoffUtc";
        var rows = await c.QueryAsync<Row>(new CommandDefinition(sql, new { TeamId = teamId, CompetitionId = competitionId, SeasonId = seasonId, CutoffUtc = cutoffUtc.UtcDateTime, Context = (int)context }, cancellationToken: cancellationToken));
        return rows.Select(x => x.ToFeature()).ToArray();
    }
    private async Task<FeatureFixture?> QueryOneAsync(string where, object values, CancellationToken token) { await using var c=(SqlConnection)connections.CreateConnection(); await c.OpenAsync(token); var x=await c.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(Select+" "+where, values, cancellationToken:token)); return x?.ToFeature(); }
    private const string Select = """SELECT f.Id FixtureId,f.CompetitionId,f.SeasonId,f.HomeTeamId,f.AwayTeamId,f.KickoffUtc,f.Status,f.HomeScore,f.AwayScore,ss.Id SnapshotId,ss.AvailableAtUtc SnapshotAvailableAtUtc,ss.IngestedAtUtc SnapshotIngestedAtUtc,ss.IngestionRunId SnapshotIngestionRunId,ts.Goals,ts.Shots,ts.ShotsOnTarget,ts.Corners,ts.Fouls,ts.YellowCards,ts.RedCards FROM dbo.Fixtures f CROSS APPLY (SELECT TOP(1) s.* FROM dbo.FixtureStatisticsSnapshots s WHERE s.FixtureId=f.Id AND ((@Context=0 AND s.AvailableAtUtc<=@CutoffUtc AND s.IngestedAtUtc<=@CutoffUtc) OR @Context=1) ORDER BY CASE WHEN @Context=0 THEN s.AvailableAtUtc END DESC, CASE WHEN @Context=0 THEN s.IngestedAtUtc END DESC, CASE WHEN @Context=1 THEN s.IngestedAtUtc END ASC, s.Id ASC) ss INNER JOIN dbo.TeamMatchStatistics ts ON ts.FixtureStatisticsSnapshotId=ss.Id AND ts.TeamId=@TeamId""";
    private sealed class Row { public Guid FixtureId {get;init;} public Guid CompetitionId{get;init;} public Guid SeasonId{get;init;} public Guid HomeTeamId{get;init;} public Guid AwayTeamId{get;init;} public DateTime KickoffUtc{get;init;} public byte Status{get;init;} public int? HomeScore{get;init;} public int? AwayScore{get;init;} public Guid SnapshotId{get;init;} public DateTime SnapshotAvailableAtUtc{get;init;} public DateTime SnapshotIngestedAtUtc{get;init;} public Guid SnapshotIngestionRunId{get;init;} public int? Goals{get;init;} public int? Shots{get;init;} public int? ShotsOnTarget{get;init;} public int? Corners{get;init;} public int? Fouls{get;init;} public int? YellowCards{get;init;} public int? RedCards{get;init;} public FeatureFixture ToFeature()=>new(FixtureId,CompetitionId,SeasonId,HomeTeamId,AwayTeamId,new(DateTime.SpecifyKind(KickoffUtc,DateTimeKind.Utc)),Status,HomeScore,AwayScore,SnapshotId,new(DateTime.SpecifyKind(SnapshotAvailableAtUtc,DateTimeKind.Utc)),new(DateTime.SpecifyKind(SnapshotIngestedAtUtc,DateTimeKind.Utc)),SnapshotIngestionRunId,Goals,Shots,ShotsOnTarget,Corners,Fouls,YellowCards,RedCards); }
}
