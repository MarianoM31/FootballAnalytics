using FootballAnalytics.Application.Ingestion;
using FootballAnalytics.Domain.Entities;
using FootballAnalytics.Domain.Temporal;

namespace FootballAnalytics.Application.Historical;

public sealed class HistoricalStatisticsIngestionService(
    ICompetitionRepository competitions, ISeasonRepository seasons, ITeamRepository teams, IFixtureRepository fixtures,
    IIngestionRunRepository ingestionRuns, IFixtureStatisticsSnapshotRepository snapshots, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<HistoricalIngestionResult> SyncAsync(IHistoricalMatchStatisticsProvider provider, string competitionExternalId, string seasonExternalId, CancellationToken cancellationToken = default)
    {
        var started = timeProvider.GetUtcNow();
        var runId = await ingestionRuns.StartAsync(provider.ProviderCode, started, cancellationToken);
        try
        {
            var matches = await provider.GetMatchesAsync(competitionExternalId, seasonExternalId, cancellationToken);
            if (matches.Count == 0) throw new InvalidOperationException("The historical provider returned no matches.");
            var first = matches[0];
            var competitionId = await competitions.UpsertAsync(provider.ProviderCode, new(first.CompetitionExternalId, first.CompetitionName, first.CompetitionCountry), cancellationToken);
            var seasonId = await seasons.UpsertAsync(provider.ProviderCode, new(first.SeasonExternalId, first.CompetitionExternalId, first.SeasonName, first.SeasonStartDate, first.SeasonEndDate), competitionId, cancellationToken);
            var teamIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
            foreach (var team in matches.SelectMany(x => new[] { (x.HomeTeamExternalId, x.HomeTeamName), (x.AwayTeamExternalId, x.AwayTeamName) }).Distinct())
                teamIds[team.Item1] = await teams.UpsertAsync(provider.ProviderCode, new(team.Item1, team.Item2), cancellationToken);
            var created = 0;
            foreach (var match in matches)
            {
                var fixtureId = await fixtures.UpsertAsync(provider.ProviderCode, new(match.MatchExternalId, match.CompetitionExternalId, match.SeasonExternalId, match.HomeTeamExternalId, match.AwayTeamExternalId, match.KickoffUtc, match.Status, match.HomeScore, match.AwayScore), competitionId, seasonId, teamIds[match.HomeTeamExternalId], teamIds[match.AwayTeamExternalId], cancellationToken);
                var now = timeProvider.GetUtcNow();
                var snapshot = new FixtureStatisticsSnapshot(Guid.NewGuid(), fixtureId, provider.ProviderCode, null, TemporalObservation.ResolveAvailableAtUtc(null, now), now, runId, StatsBombContentFingerprint.For(match));
                var statistics = match.TeamStatistics.Select(x => new TeamMatchStatistics(Guid.NewGuid(), snapshot.Id, teamIds[x.TeamExternalId], x.Goals, x.Shots, x.ShotsOnTarget, x.PossessionPercentage, x.Corners, x.Offsides, x.Fouls, x.YellowCards, x.RedCards)).ToArray();
                if (statistics.Length != 2) throw new InvalidOperationException("A historical match must contain exactly two team statistics rows.");
                if (await snapshots.AddIfAbsentAsync(snapshot, statistics, cancellationToken)) created++;
            }
            var result = new IngestionResult(runId, 1, 1, teamIds.Count, matches.Count);
            await ingestionRuns.CompleteAsync(runId, result, timeProvider.GetUtcNow(), cancellationToken);
            return new(runId, 1, 1, teamIds.Count, matches.Count, created);
        }
        catch (Exception exception)
        {
            await ingestionRuns.FailAsync(runId, exception.Message[..Math.Min(500, exception.Message.Length)], timeProvider.GetUtcNow(), CancellationToken.None);
            throw;
        }
    }
}
