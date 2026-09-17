namespace FootballAnalytics.Application.Ingestion;

public sealed record IngestionResult(
    Guid RunId,
    int CompetitionsProcessed,
    int SeasonsProcessed,
    int TeamsProcessed,
    int FixturesProcessed);
