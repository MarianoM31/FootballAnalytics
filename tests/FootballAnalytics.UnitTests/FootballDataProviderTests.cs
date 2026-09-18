using System.Net;
using System.Net.Http.Headers;
using FootballAnalytics.Domain.Enums;
using FootballAnalytics.Infrastructure.FootballData;
using Microsoft.Extensions.Options;

namespace FootballAnalytics.UnitTests;

public sealed class FootballDataProviderTests
{
    [Fact]
    public async Task Provider_maps_competition_season_team_and_fixture_to_normalized_models()
    {
        var handler = new StubHandler(request => request.RequestUri!.PathAndQuery switch
        {
            "/v4/competitions/PL" => Json("""{"id":2021,"name":"Premier League","area":{"name":"England"},"currentSeason":{"id":900,"startDate":"2026-08-01","endDate":"2027-05-31"}}"""),
            "/v4/competitions/PL/teams" => Json("""{"teams":[{"id":1,"name":"Arsenal"},{"id":2,"name":"Liverpool"}]}"""),
            var path when path.StartsWith("/v4/competitions/PL/matches?", StringComparison.Ordinal) => Json("""{"matches":[{"id":99,"utcDate":"2026-09-20T18:00:00Z","status":"SCHEDULED","competition":{"id":2021},"season":{"id":900},"homeTeam":{"id":1},"awayTeam":{"id":2},"score":{"fullTime":{"home":null,"away":null}}}]}"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        var provider = CreateProvider(handler);

        var competitions = await provider.GetCompetitionsAsync(CancellationToken.None);
        var seasons = await provider.GetSeasonsAsync(CancellationToken.None);
        var teams = await provider.GetTeamsAsync(CancellationToken.None);
        var fixtures = await provider.GetFixturesAsync(FromUtc, ToUtc, CancellationToken.None);

        Assert.Equal(new("2021", "Premier League", "England"), Assert.Single(competitions));
        Assert.Equal("900", Assert.Single(seasons).ExternalId);
        Assert.Equal(2, teams.Count);
        var fixture = Assert.Single(fixtures);
        Assert.Equal("99", fixture.ExternalId);
        Assert.Equal(FixtureStatus.Scheduled, fixture.Status);
        Assert.Equal(TimeSpan.Zero, fixture.KickoffUtc.Offset);
        Assert.All(handler.Requests, request => Assert.True(request.Headers.Contains("X-Auth-Token")));
    }

    [Theory]
    [InlineData("SCHEDULED", FixtureStatus.Scheduled)]
    [InlineData("IN_PLAY", FixtureStatus.InProgress)]
    [InlineData("FINISHED", FixtureStatus.Finished)]
    [InlineData("POSTPONED", FixtureStatus.Postponed)]
    [InlineData("CANCELLED", FixtureStatus.Cancelled)]
    public void Status_mapper_maps_known_statuses(string externalStatus, FixtureStatus expected) =>
        Assert.Equal(expected, FootballDataFixtureStatusMapper.Map(externalStatus));

    [Fact]
    public void Status_mapper_rejects_unknown_status() =>
        Assert.Throws<FootballDataProviderException>(() => FootballDataFixtureStatusMapper.Map("UNKNOWN"));

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "rejected")]
    [InlineData(HttpStatusCode.Forbidden, "rejected")]
    public async Task Provider_reports_authentication_and_authorization_errors(HttpStatusCode statusCode, string expectedMessage)
    {
        var provider = CreateProvider(new StubHandler(_ => new HttpResponseMessage(statusCode)));
        var exception = await Assert.ThrowsAsync<FootballDataProviderException>(() => provider.GetCompetitionsAsync(CancellationToken.None));
        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Provider_reports_rate_limits_without_retrying()
    {
        var response = new HttpResponseMessage((HttpStatusCode)429);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
        var handler = new StubHandler(_ => response);
        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<FootballDataProviderException>(() => provider.GetCompetitionsAsync(CancellationToken.None));

        Assert.Contains("Retry after 30 seconds", exception.Message);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Provider_reports_invalid_json()
    {
        var provider = CreateProvider(new StubHandler(_ => Json("not-json")));

        var exception = await Assert.ThrowsAsync<FootballDataProviderException>(() => provider.GetCompetitionsAsync(CancellationToken.None));

        Assert.Contains("invalid JSON", exception.Message);
    }

    [Fact]
    public async Task Provider_rejects_non_utc_fixture_kickoff()
    {
        var handler = new StubHandler(request => request.RequestUri!.PathAndQuery.StartsWith("/v4/competitions/PL/matches?", StringComparison.Ordinal)
            ? Json("""{"matches":[{"id":99,"utcDate":"2026-09-20T18:00:00+02:00","status":"SCHEDULED","competition":{"id":2021},"season":{"id":900},"homeTeam":{"id":1},"awayTeam":{"id":2}}]}""")
            : new HttpResponseMessage(HttpStatusCode.NotFound));
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<FootballDataProviderException>(() => provider.GetFixturesAsync(FromUtc, ToUtc, CancellationToken.None));
    }

    private static readonly DateTimeOffset FromUtc = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ToUtc = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static FootballDataProvider CreateProvider(HttpMessageHandler handler) =>
        new(new HttpClient(handler), Options.Create(new FootballDataOptions { BaseUrl = "https://api.football-data.org/v4/", ApiToken = "test-token" }));

    private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK) { Content = new StringContent(content) };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responseFactory(request));
        }
    }
}
