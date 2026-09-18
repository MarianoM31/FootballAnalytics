using System.Net;
using System.Text;
using System.Text.Json;
using FootballAnalytics.Application.Historical;
using FootballAnalytics.Infrastructure.StatsBomb;

namespace FootballAnalytics.UnitTests;

public sealed class StatsBombPhase2BTests
{
    [Fact]
    public void Calculator_applies_approved_event_semantics()
    {
        using var events = JsonDocument.Parse("""
        [{"team":{"id":"Home"},"type":{"name":"Shot"},"shot":{"outcome":{"name":"Goal"}}},{"team":{"id":"Home"},"type":{"name":"Shot"},"shot":{"outcome":{"name":"Saved To Post"}}},{"team":{"id":"Home"},"type":{"name":"Shot"},"shot":{"outcome":{"name":"Post"}}},{"team":{"id":"Home"},"type":{"name":"Pass"},"pass":{"type":{"name":"Corner"}}},{"team":{"id":"Home"},"type":{"name":"Pass"},"pass":{"outcome":{"name":"Pass Offside"}}},{"team":{"id":"Home"},"type":{"name":"Foul Committed"},"foul_committed":{"card":{"name":"Second Yellow"}}},{"team":{"id":"Away"},"type":{"name":"Own Goal For"} }]
        """);
        var actual = new StatsBombEventStatisticsCalculator().Calculate(events.RootElement, "Home", "Away", 1, 1, "1").OrderBy(x => x.TeamExternalId).ToArray();
        var home = actual[1];
        Assert.Equal(3, home.Shots); Assert.Equal(2, home.ShotsOnTarget); Assert.Equal(1, home.Corners); Assert.Equal(1, home.Offsides); Assert.Equal(1, home.Fouls); Assert.Equal(1, home.YellowCards); Assert.Equal(1, home.RedCards); Assert.Null(home.PossessionPercentage);
    }

    [Fact]
    public void Calculator_excludes_penalty_shootout_goals()
    {
        using var events = JsonDocument.Parse("""[{"team":{"id":"Home"},"period":5,"type":{"name":"Shot"},"shot":{"outcome":{"name":"Goal"}}}]""");
        var actual = new StatsBombEventStatisticsCalculator().Calculate(events.RootElement, "Home", "Away", 0, 0, "1");
        Assert.Equal(0, actual.Single(x => x.TeamExternalId == "Home").Shots);
    }

    [Fact]
    public void Calculator_surfaces_score_discrepancy()
    {
        using var events = JsonDocument.Parse("""[]""");
        Assert.Throws<StatsBombOpenDataException>(() => new StatsBombEventStatisticsCalculator().Calculate(events.RootElement, "Home", "Away", 1, 0, "1"));
    }

    [Fact]
    public void Fingerprint_is_stable_without_local_timestamps_or_guids()
    {
        var first = Match("1", 1); var second = Match("1", 1); var corrected = Match("1", 2);
        Assert.Equal(StatsBombContentFingerprint.For(first), StatsBombContentFingerprint.For(second));
        Assert.NotEqual(StatsBombContentFingerprint.For(first), StatsBombContentFingerprint.For(corrected));
    }

    [Fact]
    public async Task Provider_maps_metadata_and_events()
    {
        var responses = new Dictionary<string, string> {
            ["competitions.json"] = """[{"competition_id":43,"season_id":3,"competition_name":"FIFA World Cup","country_name":"International","season_name":"2018"}]""",
            ["matches/43/3.json"] = """[{"match_id":1,"match_date":"2018-06-01","kick_off":"12:00:00.000","home_team":{"home_team_id":10,"home_team_name":"Home"},"away_team":{"away_team_id":20,"away_team_name":"Away"},"home_score":1,"away_score":0}]""",
            ["events/1.json"] = """[{"team":{"id":10},"type":{"name":"Shot"},"shot":{"outcome":{"name":"Goal"}}}]""" };
        var client = new HttpClient(new Handler(responses)) { BaseAddress = new Uri("https://example.test/") };
        var matches = await new StatsBombOpenDataProvider(client, new()).GetMatchesAsync("43", "3", CancellationToken.None);
        Assert.Single(matches); Assert.Equal("1", matches[0].MatchExternalId); Assert.Equal(1, matches[0].TeamStatistics.Single(x => x.TeamExternalId == "10").Goals);
    }

    [Fact]
    public async Task Provider_reports_http_and_json_failures()
    {
        var http = new HttpClient(new Handler(new())) { BaseAddress = new Uri("https://example.test/") };
        await Assert.ThrowsAsync<StatsBombOpenDataException>(() => new StatsBombOpenDataProvider(http, new()).GetMatchesAsync("43", "3", CancellationToken.None));
        var invalid = new HttpClient(new Handler(new() { ["competitions.json"] = "not-json" })) { BaseAddress = new Uri("https://example.test/") };
        await Assert.ThrowsAsync<StatsBombOpenDataException>(() => new StatsBombOpenDataProvider(invalid, new()).GetMatchesAsync("43", "3", CancellationToken.None));
    }

    [Fact]
    public async Task Provider_bounds_event_downloads_and_returns_deterministic_order()
    {
        var responses = new Dictionary<string, string> {
            ["competitions.json"] = """[{"competition_id":43,"season_id":3,"competition_name":"FIFA World Cup","season_name":"2018"}]""",
            ["matches/43/3.json"] = """[{"match_id":2,"match_date":"2018-06-02","kick_off":"12:00:00.000","home_team":{"home_team_id":10,"home_team_name":"Home"},"away_team":{"away_team_id":20,"away_team_name":"Away"},"home_score":0,"away_score":0},{"match_id":1,"match_date":"2018-06-01","kick_off":"12:00:00.000","home_team":{"home_team_id":10,"home_team_name":"Home"},"away_team":{"away_team_id":20,"away_team_name":"Away"},"home_score":0,"away_score":0},{"match_id":3,"match_date":"2018-06-03","kick_off":"12:00:00.000","home_team":{"home_team_id":10,"home_team_name":"Home"},"away_team":{"away_team_id":20,"away_team_name":"Away"},"home_score":0,"away_score":0},{"match_id":4,"match_date":"2018-06-04","kick_off":"12:00:00.000","home_team":{"home_team_id":10,"home_team_name":"Home"},"away_team":{"away_team_id":20,"away_team_name":"Away"},"home_score":0,"away_score":0},{"match_id":5,"match_date":"2018-06-05","kick_off":"12:00:00.000","home_team":{"home_team_id":10,"home_team_name":"Home"},"away_team":{"away_team_id":20,"away_team_name":"Away"},"home_score":0,"away_score":0}]""" };
        foreach (var id in Enumerable.Range(1, 5)) responses[$"events/{id}.json"] = "[]";
        var handler = new TrackingHandler(responses);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var actual = await new StatsBombOpenDataProvider(client, new()).GetMatchesAsync("43", "3", CancellationToken.None);
        Assert.Equal(["1", "2", "3", "4", "5"], actual.Select(x => x.MatchExternalId));
        Assert.InRange(handler.MaximumActiveEventRequests, 1, StatsBombOpenDataProvider.MaximumEventDownloadConcurrency);
    }

    [Fact]
    public async Task Provider_honors_cancellation()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        var client = new HttpClient(new Handler(new())) { BaseAddress = new Uri("https://example.test/") };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new StatsBombOpenDataProvider(client, new()).GetMatchesAsync("43", "3", source.Token));
    }

    private static NormalizedHistoricalMatch Match(string id, int shots) => new("43", "World Cup", "International", "3", "2018", new(2018, 6, 1), new(2018, 7, 1), id, "10", "Home", "20", "Away", DateTimeOffset.UnixEpoch, FootballAnalytics.Domain.Enums.FixtureStatus.Finished, 1, 0, [new("10", 1, shots, 1, null, 0, 0, 0, 0, 0), new("20", 0, 0, 0, null, 0, 0, 0, 0, 0)], 1, 0);
    private sealed class Handler(Dictionary<string, string> responses) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(responses.TryGetValue(request.RequestUri!.AbsolutePath.TrimStart('/'), out var json)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") }
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
    private sealed class TrackingHandler(Dictionary<string, string> responses) : HttpMessageHandler
    {
        private int active;
        public int MaximumActiveEventRequests { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath.TrimStart('/');
            if (path.StartsWith("events/", StringComparison.Ordinal))
            {
                var current = Interlocked.Increment(ref active);
                MaximumActiveEventRequests = Math.Max(MaximumActiveEventRequests, current);
                await Task.Delay(30, cancellationToken);
                Interlocked.Decrement(ref active);
            }
            return responses.TryGetValue(path, out var json)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
