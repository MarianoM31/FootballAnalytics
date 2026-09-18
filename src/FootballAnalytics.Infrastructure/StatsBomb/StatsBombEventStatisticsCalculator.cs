using System.Text.Json;
using FootballAnalytics.Application.Historical;

namespace FootballAnalytics.Infrastructure.StatsBomb;

public sealed class StatsBombEventStatisticsCalculator
{
    public IReadOnlyList<NormalizedHistoricalTeamStatistics> Calculate(JsonElement events, string homeTeamId, string awayTeamId, int? homeScore, int? awayScore, string matchId)
    {
        var values = new Dictionary<string, Values>(StringComparer.Ordinal) { [homeTeamId] = new(), [awayTeamId] = new() };
        foreach (var item in events.EnumerateArray())
        {
            if (!TryId(item, "team", out var team) || !values.TryGetValue(team, out var value)) continue;
            var type = Name(item, "type");
            var shootout = item.TryGetProperty("period", out var period) && period.GetInt32() == 5;
            if (type == "Shot" && !shootout)
            {
                value.Shots++;
                var outcome = Name(item, "shot", "outcome");
                if (outcome is "Goal" or "Saved" or "Saved To Post") value.ShotsOnTarget++;
                if (outcome == "Goal") value.EventGoals++;
            }
            else if (type == "Own Goal For" && !shootout) value.EventGoals++;
            else if (type == "Pass" && !shootout)
            {
                if (Name(item, "pass", "type") == "Corner") value.Corners++;
                if (Name(item, "pass", "outcome") == "Pass Offside") value.Offsides++;
            }
            else if (type == "Offside" && !shootout) value.Offsides++;
            else if (type == "Foul Committed" && !shootout)
            {
                value.Fouls++;
                AddCard(value, Name(item, "foul_committed", "card"));
            }
            else if (type == "Bad Behaviour" && !shootout) AddCard(value, Name(item, "bad_behaviour", "card"));
        }
        if (homeScore.HasValue && awayScore.HasValue && (values[homeTeamId].EventGoals != homeScore || values[awayTeamId].EventGoals != awayScore))
            throw new StatsBombOpenDataException($"StatsBomb score validation failed for match {matchId}: metadata {homeScore}-{awayScore}, events {values[homeTeamId].EventGoals}-{values[awayTeamId].EventGoals}.");
        return [ToNormalized(homeTeamId, values[homeTeamId], homeScore), ToNormalized(awayTeamId, values[awayTeamId], awayScore)];
    }

    private static NormalizedHistoricalTeamStatistics ToNormalized(string id, Values x, int? goals) => new(id, goals, x.Shots, x.ShotsOnTarget, null, x.Corners, x.Offsides, x.Fouls, x.Yellows, x.Reds);
    private static void AddCard(Values x, string? card)
    {
        if (card is "Yellow Card" or "Second Yellow") x.Yellows++;
        if (card is "Red Card" or "Second Yellow") x.Reds++;
    }
    private static bool TryName(JsonElement source, string property, out string name) { name = Name(source, property) ?? string.Empty; return name.Length > 0; }
    private static bool TryId(JsonElement source, string property, out string id)
    {
        id = string.Empty;
        return source.TryGetProperty(property, out var value) && value.TryGetProperty("id", out var raw) && (id = raw.ToString()).Length > 0;
    }
    private static string? Name(JsonElement source, params string[] path)
    {
        var current = source;
        foreach (var part in path)
            if (!current.TryGetProperty(part, out current)) return null;
        return current.TryGetProperty("name", out var name) ? name.GetString() : null;
    }
    private sealed class Values { public int Shots; public int ShotsOnTarget; public int Corners; public int Offsides; public int Fouls; public int Yellows; public int Reds; public int EventGoals; }
}
