using Dapper;
using FootballAnalytics.Application.Features;
using FootballAnalytics.Infrastructure.Features;
using FootballAnalytics.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace FootballAnalytics.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class FeatureEngineSqlTests
{
    [Theory]
    [InlineData("Argentina", 3, false)]
    [InlineData("Belgium", 5, true)]
    public async Task Historical_research_uses_only_prior_same_season_matches(string opponent, int expectedSample, bool complete)
    {
        var config = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json").Build();
        var factory = new SqlConnectionFactory(config.GetConnectionString("FootballAnalyticsDb")!);
        await using var connection = (SqlConnection)factory.CreateConnection(); await connection.OpenAsync();
        var target = await connection.QuerySingleAsync<(Guid FixtureId, Guid TeamId, DateTime KickoffUtc)>("SELECT f.Id FixtureId, f.HomeTeamId TeamId, f.KickoffUtc FROM dbo.Fixtures f JOIN dbo.Teams h ON h.Id=f.HomeTeamId JOIN dbo.Teams a ON a.Id=f.AwayTeamId WHERE h.Name='France' AND a.Name=@Opponent", new { Opponent = opponent });
        var service = new TeamFeatureService(new DapperTemporalFeatureDataRepository(factory));
        var result = await service.CalculateAsync(new(target.FixtureId, target.TeamId, new DateTimeOffset(DateTime.SpecifyKind(target.KickoffUtc, DateTimeKind.Utc)).AddSeconds(-1), TemporalContext.HistoricalResearch, FeatureWindow.Last5));
        Assert.Equal(expectedSample, result.SampleSize); Assert.Equal(5, result.RequestedSampleSize); Assert.Equal(complete, result.IsCompleteWindow); Assert.Equal(expectedSample, result.SourceFixtureIds.Distinct().Count()); Assert.Equal(expectedSample, result.SourceSnapshotIds.Distinct().Count()); Assert.NotEmpty(result.Fingerprint);
    }
}
