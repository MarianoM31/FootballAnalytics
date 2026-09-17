namespace FootballAnalytics.Domain.Identifiers;

/// <summary>Maps a domain-owned identifier to an identifier supplied by a data provider.</summary>
public sealed record ProviderIdentifier(
    Guid InternalEntityId,
    string Provider,
    string ExternalId,
    ExternalEntityType EntityType);
