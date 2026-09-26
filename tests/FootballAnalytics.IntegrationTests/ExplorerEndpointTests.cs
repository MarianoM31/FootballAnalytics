using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using FootballAnalytics.Application.Explorer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace FootballAnalytics.IntegrationTests;

// In-memory repository: these endpoint tests never connect to SQL or a provider.
public sealed class ExplorerEndpointTests
{
    [Fact]
    public async Task Catalogue_preserves_partial_coverage_counts()
    {
        using var factory = new Factory(new Repository());
        var rows = await factory.CreateClient().GetFromJsonAsync<SeasonSummary[]>("/api/explorer/competitions");
        var season = Assert.Single(rows!);
        Assert.Equal(35, season.MatchCount);
        Assert.Equal(2, season.StatisticsMatchCount);
    }

    [Theory]
    [InlineData("x")]
    [InlineData(" X ")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Catalogue_flags_invalid_names_without_replacing_source_values(string name)
    {
        using var factory = new Factory(new Repository { CompetitionName = name });
        var rows = await factory.CreateClient().GetFromJsonAsync<SeasonSummary[]>("/api/explorer/competitions");
        var season = Assert.Single(rows!);
        Assert.Equal(name, season.CompetitionName);
        Assert.False(season.IsSelectable);
        Assert.Equal("Identidad pendiente de verificación", season.IdentityWarning);
        // Assert the API actually serializes the eligibility metadata consumed by Web.
        using var json = System.Text.Json.JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/explorer/competitions"));
        Assert.False(json.RootElement[0].GetProperty("isSelectable").GetBoolean());
        Assert.Equal(season.IdentityWarning, json.RootElement[0].GetProperty("identityWarning").GetString());
    }

    [Theory]
    [InlineData("FIFA World Cup")]
    [InlineData("Premier League")]
    [InlineData("Liga X")]
    public async Task Catalogue_keeps_named_competitions_selectable_without_a_whitelist(string name)
    {
        using var factory = new Factory(new Repository { CompetitionName = name });
        var rows = await factory.CreateClient().GetFromJsonAsync<SeasonSummary[]>("/api/explorer/competitions");
        var season = Assert.Single(rows!);
        Assert.Equal(name, season.CompetitionName);
        Assert.True(season.IsSelectable);
        Assert.Null(season.IdentityWarning);
    }

    [Fact]
    public async Task Matches_are_bounded_and_preserve_missing_results()
    {
        var repository = new Repository();
        using var factory = new Factory(repository);
        var page = await factory.CreateClient().GetFromJsonAsync<MatchPage>($"/api/explorer/matches?seasonId={Repository.SeasonId}&page=2");
        Assert.Equal(24, page!.Items.Count);
        Assert.True(page.HasMore);
        Assert.Equal(2, page.Page);
        Assert.Equal(24, repository.Offset);
        Assert.Equal(25, repository.Take);
        Assert.All(page.Items, match => { Assert.Null(match.HomeScore); Assert.Equal(0, match.AwayScore); });
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("page=10001")]
    [InlineData("page=oops")]
    public async Task Invalid_page_is_rejected_before_reading(string query)
    {
        var repository = new Repository();
        using var factory = new Factory(repository);
        var response = await factory.CreateClient().GetAsync($"/api/explorer/matches?seasonId={Repository.SeasonId}&{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(repository.Take);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("not-a-guid")]
    public async Task Invalid_season_is_rejected(string season)
    {
        using var factory = new Factory(new Repository());
        Assert.Equal(HttpStatusCode.BadRequest, (await factory.CreateClient().GetAsync($"/api/explorer/matches?seasonId={season}")).StatusCode);
    }

    [Fact]
    public async Task Detail_preserves_null_zero_provenance_and_observation_times()
    {
        using var factory = new Factory(new Repository());
        var detail = await factory.CreateClient().GetFromJsonAsync<MatchDetail>($"/api/explorer/matches/{Repository.MatchId}");
        var snapshot = Assert.Single(detail!.Snapshots);
        Assert.Equal("statsbomb-open", snapshot.Provider);
        Assert.True(snapshot.IngestedAtUtc > detail.Match.KickoffUtc);
        Assert.Equal(TimeSpan.Zero, snapshot.IngestedAtUtc.Offset);
        var team = Assert.Single(snapshot.Teams);
        Assert.Null(team.PossessionPercentage);
        Assert.Null(team.Shots);
        Assert.Equal(0, team.Corners);
    }

    [Fact]
    public async Task Missing_match_is_404_and_match_without_statistics_is_not_fabricated()
    {
        using var factory = new Factory(new Repository { WithoutStatistics = true });
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/explorer/matches/{Guid.NewGuid()}")).StatusCode);
        var detail = await client.GetFromJsonAsync<MatchDetail>($"/api/explorer/matches/{Repository.MatchId}");
        Assert.Empty(detail!.Snapshots);
    }

    [Fact]
    public async Task Database_failure_is_503_without_connection_details()
    {
        using var factory = new Factory(new Repository { Fail = true });
        var response = await factory.CreateClient().GetAsync("/api/explorer/competitions");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private-server", body);
    }

    private sealed class Factory(IExplorerRepository repository) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureServices(services =>
        {
            services.RemoveAll<IExplorerRepository>();
            services.AddSingleton(repository);
        });
    }

    private sealed class Repository : IExplorerRepository
    {
        public static readonly Guid SeasonId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public static readonly Guid MatchId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly DateTimeOffset Kickoff = new(2018, 7, 15, 15, 0, 0, TimeSpan.Zero);
        private static readonly MatchSummary Match = new(MatchId, "Local", "Visitante", Kickoff, 2, null, 0, true);
        public int? Offset { get; private set; }
        public int? Take { get; private set; }
        public bool WithoutStatistics { get; init; }
        public bool Fail { get; init; }
        public string CompetitionName { get; init; } = "Competición de prueba";

        public Task<IReadOnlyList<SeasonSummary>> GetSeasonsAsync(CancellationToken cancellationToken) =>
            Fail ? throw new UnavailableException() : Task.FromResult<IReadOnlyList<SeasonSummary>>(
                [new(Guid.NewGuid(), CompetitionName, null, SeasonId, "2018", 35, 2)]);

        public Task<IReadOnlyList<MatchSummary>> GetMatchesAsync(Guid seasonId, int offset, int take, CancellationToken cancellationToken)
        {
            Offset = offset; Take = take;
            return Task.FromResult<IReadOnlyList<MatchSummary>>(Enumerable.Range(0, take).Select(_ => Match).ToArray());
        }

        public Task<MatchDetail?> GetMatchAsync(Guid matchId, CancellationToken cancellationToken)
        {
            var snapshotId = Guid.NewGuid();
            return Task.FromResult<MatchDetail?>(matchId != MatchId ? null : new(Match,
                WithoutStatistics ? [] : [new(snapshotId, "statsbomb-open", Kickoff.AddYears(8), Kickoff.AddYears(8),
                    [new(snapshotId, "Local", true, 0, null, 0, null, 0, null, null, 0, 0)])]));
        }
    }

    private sealed class UnavailableException() : DbException("private-server connection failed");
}
