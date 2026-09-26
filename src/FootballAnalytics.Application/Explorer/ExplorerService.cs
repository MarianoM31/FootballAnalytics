namespace FootballAnalytics.Application.Explorer;

public sealed record SeasonSummary(Guid CompetitionId, string CompetitionName, string? Country,
    Guid SeasonId, string SeasonName, int MatchCount, int StatisticsMatchCount)
{
    // Keep the source name intact. "x" is a confirmed placeholder in existing test records.
    // This narrow quality check is not a whitelist or proof of provider identity.
    public bool IsSelectable => !string.IsNullOrWhiteSpace(CompetitionName)
        && !string.Equals(CompetitionName.Trim(), "x", StringComparison.OrdinalIgnoreCase);
    public string? IdentityWarning => IsSelectable ? null : "Identidad pendiente de verificación";
}
public sealed record MatchSummary(Guid Id, string HomeTeam, string AwayTeam, DateTimeOffset KickoffUtc,
    byte Status, int? HomeScore, int? AwayScore, bool HasStatistics);
public sealed record MatchPage(IReadOnlyList<MatchSummary> Items, int Page, bool HasMore);
public sealed record TeamStatistics(Guid SnapshotId, string TeamName, bool IsHome, int? Goals,
    int? Shots, int? ShotsOnTarget, decimal? PossessionPercentage, int? Corners, int? Offsides,
    int? Fouls, int? YellowCards, int? RedCards);
public sealed record StatisticsSnapshot(Guid Id, string Provider, DateTimeOffset AvailableAtUtc,
    DateTimeOffset IngestedAtUtc, IReadOnlyList<TeamStatistics> Teams);
public sealed record MatchDetail(MatchSummary Match, IReadOnlyList<StatisticsSnapshot> Snapshots);

public interface IExplorerRepository
{
    Task<IReadOnlyList<SeasonSummary>> GetSeasonsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<MatchSummary>> GetMatchesAsync(Guid seasonId, int offset, int take, CancellationToken cancellationToken);
    Task<MatchDetail?> GetMatchAsync(Guid matchId, CancellationToken cancellationToken);
}

/// <summary>Read-only exploration of stored data, not a historical as-of reconstruction.</summary>
public sealed class ExplorerService(IExplorerRepository repository)
{
    public const int PageSize = 24;
    public Task<IReadOnlyList<SeasonSummary>> GetSeasonsAsync(CancellationToken cancellationToken) =>
        repository.GetSeasonsAsync(cancellationToken);

    public async Task<MatchPage> GetMatchesAsync(Guid seasonId, int page, CancellationToken cancellationToken)
    {
        if (seasonId == Guid.Empty || page is < 1 or > 10000)
            throw new ArgumentException("La temporada y la página deben ser válidas (1–10000).");
        var rows = await repository.GetMatchesAsync(seasonId, (page - 1) * PageSize, PageSize + 1, cancellationToken);
        return new(rows.Take(PageSize).ToArray(), page, rows.Count > PageSize);
    }

    public Task<MatchDetail?> GetMatchAsync(Guid matchId, CancellationToken cancellationToken) =>
        repository.GetMatchAsync(matchId, cancellationToken);
}
