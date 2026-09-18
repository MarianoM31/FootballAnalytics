namespace FootballAnalytics.Domain.Entities;

public sealed record TeamMatchStatistics(
    Guid Id,
    Guid FixtureStatisticsSnapshotId,
    Guid TeamId,
    int? Goals,
    int? Shots,
    int? ShotsOnTarget,
    decimal? PossessionPercentage,
    int? Corners,
    int? Offsides,
    int? Fouls,
    int? YellowCards,
    int? RedCards);
