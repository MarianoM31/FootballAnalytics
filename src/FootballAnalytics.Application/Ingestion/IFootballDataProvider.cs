namespace FootballAnalytics.Application.Ingestion;

public interface IFootballDataProvider
{
    string ProviderCode { get; }

    Task<IReadOnlyList<NormalizedCompetition>> GetCompetitionsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<NormalizedSeason>> GetSeasonsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<NormalizedTeam>> GetTeamsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<NormalizedFixture>> GetFixturesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);
}
