namespace FootballAnalytics.Infrastructure.FootballData;

public sealed class FootballDataOptions
{
    public const string SectionName = "FootballData";

    public string BaseUrl { get; init; } = "https://api.football-data.org/v4/";

    public string? ApiToken { get; init; }
}
