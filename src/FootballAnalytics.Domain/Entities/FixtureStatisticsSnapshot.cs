namespace FootballAnalytics.Domain.Entities;

public sealed record FixtureStatisticsSnapshot(
    Guid Id,
    Guid FixtureId,
    string Provider,
    DateTimeOffset? ObservedAtUtc,
    DateTimeOffset AvailableAtUtc,
    DateTimeOffset IngestedAtUtc,
    Guid IngestionRunId,
    string SourceFingerprint);
