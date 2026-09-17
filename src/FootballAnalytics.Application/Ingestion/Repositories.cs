using FootballAnalytics.Domain.Identifiers;

namespace FootballAnalytics.Application.Ingestion;

public interface ICompetitionRepository
{
    Task<Guid> UpsertAsync(string providerCode, NormalizedCompetition competition, CancellationToken cancellationToken);
}

public interface ISeasonRepository
{
    Task<Guid> UpsertAsync(string providerCode, NormalizedSeason season, Guid competitionId, CancellationToken cancellationToken);
}

public interface ITeamRepository
{
    Task<Guid> UpsertAsync(string providerCode, NormalizedTeam team, CancellationToken cancellationToken);
}

public interface IFixtureRepository
{
    Task<Guid> UpsertAsync(
        string providerCode,
        NormalizedFixture fixture,
        Guid competitionId,
        Guid seasonId,
        Guid homeTeamId,
        Guid awayTeamId,
        CancellationToken cancellationToken);
}

public interface IProviderIdentifierRepository
{
    Task<Guid?> FindInternalEntityIdAsync(
        string providerCode,
        ExternalEntityType entityType,
        string externalId,
        CancellationToken cancellationToken);
}

public interface IIngestionRunRepository
{
    Task<Guid> StartAsync(string providerCode, DateTimeOffset startedAtUtc, CancellationToken cancellationToken);

    Task CompleteAsync(Guid id, IngestionResult result, DateTimeOffset completedAtUtc, CancellationToken cancellationToken);

    Task FailAsync(Guid id, string errorMessage, DateTimeOffset completedAtUtc, CancellationToken cancellationToken);
}
