namespace FootballAnalytics.Domain.Entities;

public sealed class Team
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
}
