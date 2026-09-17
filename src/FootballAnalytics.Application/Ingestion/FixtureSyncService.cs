namespace FootballAnalytics.Application.Ingestion;

public sealed class FixtureSyncService
{
    private readonly ICompetitionRepository competitions;
    private readonly ISeasonRepository seasons;
    private readonly ITeamRepository teams;
    private readonly IFixtureRepository fixtures;
    private readonly IIngestionRunRepository ingestionRuns;
    private readonly TimeProvider timeProvider;

    public FixtureSyncService(
        ICompetitionRepository competitions,
        ISeasonRepository seasons,
        ITeamRepository teams,
        IFixtureRepository fixtures,
        IIngestionRunRepository ingestionRuns,
        TimeProvider? timeProvider = null)
    {
        this.competitions = competitions;
        this.seasons = seasons;
        this.teams = teams;
        this.fixtures = fixtures;
        this.ingestionRuns = ingestionRuns;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IngestionResult> SyncAsync(
        IFootballDataProvider provider,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider.ProviderCode);
        EnsureUtc(fromUtc, nameof(fromUtc));
        EnsureUtc(toUtc, nameof(toUtc));

        var runId = await ingestionRuns.StartAsync(provider.ProviderCode, timeProvider.GetUtcNow(), cancellationToken);
        try
        {
            var normalizedCompetitions = await provider.GetCompetitionsAsync(cancellationToken);
            var normalizedSeasons = await provider.GetSeasonsAsync(cancellationToken);
            var normalizedTeams = await provider.GetTeamsAsync(cancellationToken);
            var normalizedFixtures = await provider.GetFixturesAsync(fromUtc, toUtc, cancellationToken);

            var competitionIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
            foreach (var competition in normalizedCompetitions)
            {
                competitionIds.Add(competition.ExternalId, await competitions.UpsertAsync(provider.ProviderCode, competition, cancellationToken));
            }

            var seasonIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
            foreach (var season in normalizedSeasons)
            {
                seasonIds.Add(season.ExternalId, await seasons.UpsertAsync(
                    provider.ProviderCode,
                    season,
                    RequiredId(competitionIds, season.CompetitionExternalId, "competition"),
                    cancellationToken));
            }

            var teamIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
            foreach (var team in normalizedTeams)
            {
                teamIds.Add(team.ExternalId, await teams.UpsertAsync(provider.ProviderCode, team, cancellationToken));
            }

            foreach (var fixture in normalizedFixtures)
            {
                EnsureUtc(fixture.KickoffUtc, nameof(fixture.KickoffUtc));
                await fixtures.UpsertAsync(
                    provider.ProviderCode,
                    fixture,
                    RequiredId(competitionIds, fixture.CompetitionExternalId, "competition"),
                    RequiredId(seasonIds, fixture.SeasonExternalId, "season"),
                    RequiredId(teamIds, fixture.HomeTeamExternalId, "home team"),
                    RequiredId(teamIds, fixture.AwayTeamExternalId, "away team"),
                    cancellationToken);
            }

            var result = new IngestionResult(runId, normalizedCompetitions.Count, normalizedSeasons.Count, normalizedTeams.Count, normalizedFixtures.Count);
            await ingestionRuns.CompleteAsync(runId, result, timeProvider.GetUtcNow(), cancellationToken);
            return result;
        }
        catch (Exception exception)
        {
            await ingestionRuns.FailAsync(runId, ToSafeErrorMessage(exception), timeProvider.GetUtcNow(), CancellationToken.None);
            throw;
        }
    }

    private static Guid RequiredId(IReadOnlyDictionary<string, Guid> ids, string externalId, string entityName) =>
        ids.TryGetValue(externalId, out var id)
            ? id
            : throw new InvalidOperationException($"The provider returned a fixture or season with an unknown {entityName} external id.");

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamps must be expressed in UTC.", parameterName);
        }
    }

    private static string ToSafeErrorMessage(Exception exception) => exception.Message.Length <= 500
        ? exception.Message
        : exception.Message[..500];
}
