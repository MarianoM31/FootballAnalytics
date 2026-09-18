using FootballAnalytics.Domain.Entities;

namespace FootballAnalytics.Domain.Validation;

public static class TeamMatchStatisticsValidator
{
    public static void Validate(TeamMatchStatistics statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        ValidateNonNegative(statistics.Goals, nameof(statistics.Goals));
        ValidateNonNegative(statistics.Shots, nameof(statistics.Shots));
        ValidateNonNegative(statistics.ShotsOnTarget, nameof(statistics.ShotsOnTarget));
        ValidateNonNegative(statistics.Corners, nameof(statistics.Corners));
        ValidateNonNegative(statistics.Offsides, nameof(statistics.Offsides));
        ValidateNonNegative(statistics.Fouls, nameof(statistics.Fouls));
        ValidateNonNegative(statistics.YellowCards, nameof(statistics.YellowCards));
        ValidateNonNegative(statistics.RedCards, nameof(statistics.RedCards));

        if (statistics.PossessionPercentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(statistics.PossessionPercentage), "Possession must be between 0 and 100.");
        }
    }

    private static void ValidateNonNegative(int? value, string fieldName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(fieldName, "Statistics cannot be negative.");
        }
    }
}
