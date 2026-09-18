using System.Globalization;
using System.Net;
using System.Text.Json;
using FootballAnalytics.Application.Ingestion;
using Microsoft.Extensions.Options;

namespace FootballAnalytics.Infrastructure.FootballData;

public sealed class FootballDataProvider : IFootballDataProvider
{
    private const string CompetitionCode = "PL";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient;
    private readonly Lazy<Task<FootballDataCompetitionDto>> competition;

    public FootballDataProvider(HttpClient httpClient, IOptions<FootballDataOptions> options)
    {
        this.httpClient = httpClient;
        var configuration = options.Value;
        if (string.IsNullOrWhiteSpace(configuration.ApiToken))
        {
            throw new InvalidOperationException("FootballData__ApiToken must be configured before using football-data.org.");
        }

        if (!Uri.TryCreate(configuration.BaseUrl, UriKind.Absolute, out var baseUrl))
        {
            throw new InvalidOperationException("FootballData__BaseUrl must be an absolute URL.");
        }

        httpClient.BaseAddress = baseUrl;
        httpClient.Timeout = TimeSpan.FromSeconds(20);
        httpClient.DefaultRequestHeaders.Remove("X-Auth-Token");
        httpClient.DefaultRequestHeaders.Add("X-Auth-Token", configuration.ApiToken);
        competition = new Lazy<Task<FootballDataCompetitionDto>>(
            () => GetJsonAsync<FootballDataCompetitionDto>($"competitions/{CompetitionCode}", CancellationToken.None));
    }

    public string ProviderCode => "football-data";

    public async Task<IReadOnlyList<NormalizedCompetition>> GetCompetitionsAsync(CancellationToken cancellationToken)
    {
        var competition = await GetCompetitionAsync(cancellationToken);
        return [new NormalizedCompetition(Id(competition.Id, "competition"), Required(competition.Name, "competition name"), competition.Area?.Name)];
    }

    public async Task<IReadOnlyList<NormalizedSeason>> GetSeasonsAsync(CancellationToken cancellationToken)
    {
        var competition = await GetCompetitionAsync(cancellationToken);
        var season = competition.CurrentSeason ?? throw new FootballDataProviderException("football-data.org did not return a current season for Premier League.");
        var startDate = season.StartDate ?? throw new FootballDataProviderException("football-data.org did not return a season start date.");
        var endDate = season.EndDate ?? throw new FootballDataProviderException("football-data.org did not return a season end date.");
        return [new NormalizedSeason(
            Id(season.Id, "season"),
            Id(competition.Id, "competition"),
            $"{startDate.Year}/{endDate.Year % 100:D2}",
            startDate,
            endDate)];
    }

    public async Task<IReadOnlyList<NormalizedTeam>> GetTeamsAsync(CancellationToken cancellationToken)
    {
        var response = await GetJsonAsync<FootballDataTeamsResponseDto>($"competitions/{CompetitionCode}/teams", cancellationToken);
        return (response.Teams ?? [])
            .Select(team => new NormalizedTeam(Id(team.Id, "team"), Required(team.Name, "team name")))
            .ToList();
    }

    public async Task<IReadOnlyList<NormalizedFixture>> GetFixturesAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        EnsureUtc(fromUtc, nameof(fromUtc));
        EnsureUtc(toUtc, nameof(toUtc));
        if (toUtc < fromUtc)
        {
            throw new ArgumentException("The fixture range end must not precede its start.", nameof(toUtc));
        }

        var query = $"competitions/{CompetitionCode}/matches?dateFrom={fromUtc:yyyy-MM-dd}&dateTo={toUtc:yyyy-MM-dd}";
        var response = await GetJsonAsync<FootballDataMatchesResponseDto>(query, cancellationToken);
        return (response.Matches ?? []).Select(MapFixture).ToList();
    }

    private async Task<FootballDataCompetitionDto> GetCompetitionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await competition.Value;
    }

    private NormalizedFixture MapFixture(FootballDataMatchDto match)
    {
        var kickoffUtc = match.UtcDate ?? throw new FootballDataProviderException("football-data.org did not return a fixture kickoff.");
        EnsureUtc(kickoffUtc, "utcDate");
        var competition = match.Competition ?? throw new FootballDataProviderException("football-data.org did not return fixture competition data.");
        var season = match.Season ?? throw new FootballDataProviderException("football-data.org did not return fixture season data.");
        var homeTeam = match.HomeTeam ?? throw new FootballDataProviderException("football-data.org did not return a home team.");
        var awayTeam = match.AwayTeam ?? throw new FootballDataProviderException("football-data.org did not return an away team.");

        return new NormalizedFixture(
            Id(match.Id, "fixture"),
            Id(competition.Id, "competition"),
            Id(season.Id, "season"),
            Id(homeTeam.Id, "home team"),
            Id(awayTeam.Id, "away team"),
            kickoffUtc,
            FootballDataFixtureStatusMapper.Map(match.Status),
            match.Score?.FullTime?.Home,
            match.Score?.FullTime?.Away);
    }

    private async Task<T> GetJsonAsync<T>(string relativePath, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(relativePath, cancellationToken);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FootballDataProviderException("football-data.org request timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new FootballDataProviderException("football-data.org could not be reached.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw CreateHttpException(response);
            }

            try
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<T>(content, SerializerOptions)
                    ?? throw new FootballDataProviderException("football-data.org returned an empty JSON response.");
            }
            catch (JsonException exception)
            {
                throw new FootballDataProviderException("football-data.org returned invalid JSON.", exception);
            }
        }
    }

    private static FootballDataProviderException CreateHttpException(HttpResponseMessage response) => response.StatusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new FootballDataProviderException("football-data.org rejected the configured token or its permissions."),
        (HttpStatusCode)429 => new FootballDataProviderException(response.Headers.RetryAfter?.Delta is { } retryAfter
            ? $"football-data.org rate limit reached. Retry after {Math.Ceiling(retryAfter.TotalSeconds)} seconds."
            : "football-data.org rate limit reached. Retry later."),
        _ when (int)response.StatusCode >= 500 => new FootballDataProviderException("football-data.org is temporarily unavailable."),
        _ => new FootballDataProviderException($"football-data.org request failed with HTTP {(int)response.StatusCode}.")
    };

    private static string Id(long id, string entityName) => id > 0
        ? id.ToString(CultureInfo.InvariantCulture)
        : throw new FootballDataProviderException($"football-data.org returned an invalid {entityName} id.");

    private static string Required(string? value, string fieldName) => !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new FootballDataProviderException($"football-data.org did not return {fieldName}.");

    private static void EnsureUtc(DateTimeOffset value, string fieldName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new FootballDataProviderException($"football-data.org returned a non-UTC {fieldName}.");
        }
    }
}
