namespace FootballAnalytics.Domain.Entities;

public sealed class Season
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CompetitionId { get; init; }
    public required string Name { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
}
