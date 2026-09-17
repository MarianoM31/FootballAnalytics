namespace FootballAnalytics.Domain.Entities;

public sealed class Competition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public string? Country { get; init; }
}
