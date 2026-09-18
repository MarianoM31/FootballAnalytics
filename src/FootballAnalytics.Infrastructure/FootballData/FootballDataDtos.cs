namespace FootballAnalytics.Infrastructure.FootballData;

internal sealed class FootballDataCompetitionDto
{
    public long Id { get; init; }
    public string? Name { get; init; }
    public FootballDataAreaDto? Area { get; init; }
    public FootballDataSeasonDto? CurrentSeason { get; init; }
}

internal sealed class FootballDataAreaDto
{
    public string? Name { get; init; }
}

internal sealed class FootballDataSeasonDto
{
    public long Id { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
}

internal sealed class FootballDataTeamDto
{
    public long Id { get; init; }
    public string? Name { get; init; }
}

internal sealed class FootballDataTeamsResponseDto
{
    public IReadOnlyList<FootballDataTeamDto>? Teams { get; init; }
}

internal sealed class FootballDataScoreDto
{
    public FootballDataScoreDetailDto? FullTime { get; init; }
}

internal sealed class FootballDataScoreDetailDto
{
    public int? Home { get; init; }
    public int? Away { get; init; }
}

internal sealed class FootballDataMatchDto
{
    public long Id { get; init; }
    public DateTimeOffset? UtcDate { get; init; }
    public string? Status { get; init; }
    public FootballDataCompetitionDto? Competition { get; init; }
    public FootballDataSeasonDto? Season { get; init; }
    public FootballDataTeamDto? HomeTeam { get; init; }
    public FootballDataTeamDto? AwayTeam { get; init; }
    public FootballDataScoreDto? Score { get; init; }
}

internal sealed class FootballDataMatchesResponseDto
{
    public IReadOnlyList<FootballDataMatchDto>? Matches { get; init; }
}
