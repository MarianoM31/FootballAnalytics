namespace FootballAnalytics.Domain.Entities;

public sealed class Player
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string FullName { get; init; }
    public DateOnly? DateOfBirth { get; init; }
}
