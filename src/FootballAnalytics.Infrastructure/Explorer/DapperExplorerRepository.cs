using Dapper;
using FootballAnalytics.Application.Explorer;
using FootballAnalytics.Application.Persistence;

namespace FootballAnalytics.Infrastructure.Explorer;

public sealed class DapperExplorerRepository(IDbConnectionFactory connections) : IExplorerRepository
{
    public async Task<IReadOnlyList<SeasonSummary>> GetSeasonsAsync(CancellationToken cancellationToken)
    {
        await using var connection = connections.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        const string sql = """
            SELECT c.Id CompetitionId, c.Name CompetitionName, c.Country,
                s.Id SeasonId, s.Name SeasonName, COUNT(f.Id) MatchCount,
                SUM(CASE WHEN coverage.FixtureId IS NOT NULL THEN 1 ELSE 0 END) StatisticsMatchCount
            FROM dbo.Competitions c
            INNER JOIN dbo.Seasons s ON s.CompetitionId = c.Id
            LEFT JOIN dbo.Fixtures f ON f.SeasonId = s.Id AND f.CompetitionId = c.Id
            LEFT JOIN (SELECT DISTINCT FixtureId FROM dbo.FixtureStatisticsSnapshots) coverage ON coverage.FixtureId = f.Id
            GROUP BY c.Id, c.Name, c.Country, s.Id, s.Name, s.StartDate
            ORDER BY c.Name, s.StartDate DESC, s.Id;
            """;
        return (await connection.QueryAsync<SeasonSummary>(new CommandDefinition(sql, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<MatchSummary>> GetMatchesAsync(Guid seasonId, int offset, int take, CancellationToken cancellationToken)
    {
        await using var connection = connections.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var sql = MatchSelect + " WHERE f.SeasonId = @SeasonId ORDER BY f.KickoffUtc DESC, f.Id OFFSET @Offset ROWS FETCH NEXT @Take ROWS ONLY;";
        return (await connection.QueryAsync<MatchSummary>(new CommandDefinition(sql,
            new { SeasonId = seasonId, Offset = offset, Take = take }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<MatchDetail?> GetMatchAsync(Guid matchId, CancellationToken cancellationToken)
    {
        await using var connection = connections.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var match = await connection.QuerySingleOrDefaultAsync<MatchSummary>(new CommandDefinition(
            MatchSelect + " WHERE f.Id = @MatchId;", new { MatchId = matchId }, cancellationToken: cancellationToken));
        if (match is null) return null;

        // Select a complete revision per provider; never merge snapshots or providers.
        const string snapshotsSql = """
            SELECT Id, Provider, TODATETIMEOFFSET(AvailableAtUtc, '+00:00') AvailableAtUtc,
                TODATETIMEOFFSET(IngestedAtUtc, '+00:00') IngestedAtUtc
            FROM (
                SELECT *, ROW_NUMBER() OVER (PARTITION BY Provider ORDER BY IngestedAtUtc DESC, Id DESC) rn
                FROM dbo.FixtureStatisticsSnapshots WHERE FixtureId = @MatchId
            ) ranked WHERE rn = 1 ORDER BY Provider;
            """;
        var snapshots = (await connection.QueryAsync<SnapshotRow>(new CommandDefinition(snapshotsSql,
            new { MatchId = matchId }, cancellationToken: cancellationToken))).AsList();
        if (snapshots.Count == 0) return new(match, []);
        const string teamsSql = """
            SELECT ts.FixtureStatisticsSnapshotId SnapshotId, t.Name TeamName,
                CAST(CASE WHEN ts.TeamId = f.HomeTeamId THEN 1 ELSE 0 END AS bit) IsHome,
                ts.Goals, ts.Shots, ts.ShotsOnTarget, ts.PossessionPercentage, ts.Corners,
                ts.Offsides, ts.Fouls, ts.YellowCards, ts.RedCards
            FROM dbo.TeamMatchStatistics ts
            INNER JOIN dbo.Teams t ON t.Id = ts.TeamId
            INNER JOIN dbo.Fixtures f ON f.Id = @MatchId
            WHERE ts.FixtureStatisticsSnapshotId IN @Ids
                AND (ts.TeamId = f.HomeTeamId OR ts.TeamId = f.AwayTeamId)
            ORDER BY IsHome DESC;
            """;
        var teams = (await connection.QueryAsync<TeamStatistics>(new CommandDefinition(teamsSql,
            new { MatchId = matchId, Ids = snapshots.Select(x => x.Id).ToArray() }, cancellationToken: cancellationToken))).AsList();
        return new(match, snapshots.Select(x => new StatisticsSnapshot(x.Id, x.Provider, x.AvailableAtUtc,
            x.IngestedAtUtc, teams.Where(t => t.SnapshotId == x.Id).ToArray())).ToArray());
    }

    private const string MatchSelect = """
        SELECT f.Id, h.Name HomeTeam, a.Name AwayTeam, TODATETIMEOFFSET(f.KickoffUtc, '+00:00') KickoffUtc,
            f.Status, f.HomeScore, f.AwayScore,
            CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.FixtureStatisticsSnapshots s WHERE s.FixtureId = f.Id)
                THEN 1 ELSE 0 END AS bit) HasStatistics
        FROM dbo.Fixtures f
        INNER JOIN dbo.Seasons season ON season.Id = f.SeasonId AND season.CompetitionId = f.CompetitionId
        INNER JOIN dbo.Teams h ON h.Id = f.HomeTeamId
        INNER JOIN dbo.Teams a ON a.Id = f.AwayTeamId
        """;

    private sealed record SnapshotRow(Guid Id, string Provider, DateTimeOffset AvailableAtUtc, DateTimeOffset IngestedAtUtc);
}
