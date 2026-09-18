using FootballAnalytics.Domain.Enums;

namespace FootballAnalytics.Domain.Entities;

public sealed record FixtureScheduleObservation(
    Guid Id,
    Guid FixtureId,
    string Provider,
    DateTimeOffset KickoffUtc,
    FixtureStatus Status,
    DateTimeOffset? ObservedAtUtc,
    DateTimeOffset AvailableAtUtc,
    DateTimeOffset IngestedAtUtc,
    Guid IngestionRunId,
    string SourceFingerprint);
