using System.Globalization;
using System.Net;
using System.Text.Json;
using FootballAnalytics.Application.Historical;
using FootballAnalytics.Domain.Enums;

namespace FootballAnalytics.Infrastructure.StatsBomb;

public sealed class StatsBombOpenDataProvider(HttpClient httpClient, StatsBombEventStatisticsCalculator calculator) : IHistoricalMatchStatisticsProvider
{
    public const int MaximumEventDownloadConcurrency = 4;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public string ProviderCode => "statsbomb-open";

    public async Task<IReadOnlyList<NormalizedHistoricalMatch>> GetMatchesAsync(string competitionExternalId, string seasonExternalId, CancellationToken cancellationToken)
    {
        if (!long.TryParse(competitionExternalId, out _) || !long.TryParse(seasonExternalId, out _)) throw new ArgumentException("StatsBomb competition and season IDs must be numeric.");
        using var competitions = await GetJsonAsync("competitions.json", cancellationToken);
        var metadata = competitions.RootElement.EnumerateArray().FirstOrDefault(x => Text(x, "competition_id") == competitionExternalId && Text(x, "season_id") == seasonExternalId);
        if (metadata.ValueKind == JsonValueKind.Undefined) throw new StatsBombOpenDataException("StatsBomb competition/season was not found in competitions.json.");
        using var matches = await GetJsonAsync($"matches/{competitionExternalId}/{seasonExternalId}.json", cancellationToken);
        var raw = matches.RootElement.EnumerateArray().ToArray();
        if (raw.Length == 0) return [];
        var dates = raw.Select(x => DateOnly.Parse(Required(x, "match_date"), CultureInfo.InvariantCulture)).ToArray();
        var country = metadata.TryGetProperty("country_name", out var c) ? c.GetString() : null;
        var competition = Required(metadata, "competition_name");
        var season = Required(metadata, "season_name");
        var output = new NormalizedHistoricalMatch[raw.Length];
        await Parallel.ForEachAsync(Enumerable.Range(0, raw.Length), new ParallelOptions
        {
            MaxDegreeOfParallelism = MaximumEventDownloadConcurrency,
            CancellationToken = cancellationToken
        }, async (index, token) =>
        {
            var match = raw[index];
            var id = Required(match, "match_id");
            var home = match.GetProperty("home_team"); var away = match.GetProperty("away_team");
            var homeId = Required(home, "home_team_id"); var awayId = Required(away, "away_team_id");
            var homeScore = NullableInt(match, "home_score"); var awayScore = NullableInt(match, "away_score");
            using var events = await GetJsonAsync($"events/{id}.json", token);
            var stats = calculator.Calculate(events.RootElement, homeId, awayId, homeScore, awayScore, id);
            output[index] = new(competitionExternalId, competition, country, seasonExternalId, season, dates.Min(), dates.Max(), id,
                homeId, Required(home, "home_team_name"), awayId, Required(away, "away_team_name"), Kickoff(match), FixtureStatus.Finished,
                homeScore, awayScore, stats, EventGoals(stats, homeId), EventGoals(stats, awayId));
        });
        return output.OrderBy(x => long.Parse(x.MatchExternalId, CultureInfo.InvariantCulture)).ToArray();
    }

    private async Task<JsonDocument> GetJsonAsync(string relativePath, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(relativePath, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new StatsBombOpenDataException($"StatsBomb Open Data request {relativePath} failed with HTTP {(int)response.StatusCode} ({response.StatusCode}).");
            return JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested) { throw new StatsBombOpenDataException("StatsBomb Open Data request timed out.", exception); }
        catch (HttpRequestException exception) { throw new StatsBombOpenDataException("StatsBomb Open Data could not be reached.", exception); }
        catch (JsonException exception) { throw new StatsBombOpenDataException($"StatsBomb Open Data returned invalid JSON for {relativePath}.", exception); }
    }
    private static string Required(JsonElement source, string property) => Text(source, property) ?? throw new StatsBombOpenDataException($"StatsBomb Open Data did not return {property}.");
    private static string? Text(JsonElement source, string property) => source.TryGetProperty(property, out var value) ? value.ToString() : null;
    private static int? NullableInt(JsonElement source, string property) => source.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetInt32() : null;
    private static DateTimeOffset Kickoff(JsonElement x) => new(DateTime.SpecifyKind(DateTime.ParseExact($"{Required(x, "match_date")} {Required(x, "kick_off")}", "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture), DateTimeKind.Utc));
    private static int? EventGoals(IEnumerable<NormalizedHistoricalTeamStatistics> values, string teamId) => values.Single(x => x.TeamExternalId == teamId).Goals;
}
