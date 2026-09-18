using System.Text.Json;
using Dapper;
using FootballAnalytics.Application.Historical;
using FootballAnalytics.Application.Ingestion;
using FootballAnalytics.DatabaseMigrator;
using FootballAnalytics.Domain.Entities;
using FootballAnalytics.Domain.Enums;
using FootballAnalytics.Infrastructure.Historical;
using FootballAnalytics.Infrastructure.Ingestion;
using FootballAnalytics.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace FootballAnalytics.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class HistoricalFoundationSqlTests : IAsyncLifetime
{
    private readonly string providerCode = $"phase2a-test-{Guid.NewGuid():N}";
    private readonly string connectionString = LoadConnectionString();

    [Fact]
    public async Task Historical_snapshots_are_idempotent_temporal_and_bound_to_fixture_teams()
    {
        var migrator = new DatabaseMigratorService(connectionString, new MigrationCatalog(Path.Combine(FindRepositoryRoot(), "database", "migrations")));
        await migrator.RunAsync();

        var factory = new SqlConnectionFactory(connectionString);
        var competitionId = await new DapperCompetitionRepository(factory).UpsertAsync(providerCode, new("competition", "Historical Test League", null), default);
        var seasonId = await new DapperSeasonRepository(factory).UpsertAsync(providerCode, new("season", "competition", "2026/27", new DateOnly(2026, 8, 1), new DateOnly(2027, 5, 31)), competitionId, default);
        var homeTeamId = await new DapperTeamRepository(factory).UpsertAsync(providerCode, new("home", "Historical Home"), default);
        var awayTeamId = await new DapperTeamRepository(factory).UpsertAsync(providerCode, new("away", "Historical Away"), default);
        var fixture = new FootballAnalytics.Application.Ingestion.NormalizedFixture("fixture", "competition", "season", "home", "away", At(18), FixtureStatus.Finished, 1, 0);
        var fixtureId = await new DapperFixtureRepository(factory).UpsertAsync(providerCode, fixture, competitionId, seasonId, homeTeamId, awayTeamId, default);
        var runId = await new DapperIngestionRunRepository(factory).StartAsync(providerCode, At(20), default);

        var statisticsRepository = new DapperFixtureStatisticsSnapshotRepository(factory);
        var snapshot = new FixtureStatisticsSnapshot(Guid.NewGuid(), fixtureId, providerCode, At(19), At(19), At(20), runId, string.Empty);
        var homeStatistics = new TeamMatchStatistics(Guid.NewGuid(), snapshot.Id, homeTeamId, 1, 10, 4, 55m, 5, null, 8, 1, null);
        var awayStatistics = new TeamMatchStatistics(Guid.NewGuid(), snapshot.Id, awayTeamId, 0, null, null, 45m, null, 2, null, null, null);
        snapshot = snapshot with { SourceFingerprint = HistoricalObservationFingerprint.ForStatistics(snapshot, [homeStatistics, awayStatistics]) };

        Assert.True(await statisticsRepository.AddIfAbsentAsync(snapshot, [homeStatistics, awayStatistics], default));
        Assert.False(await statisticsRepository.AddIfAbsentAsync(snapshot, [homeStatistics, awayStatistics], default));
        Assert.Null(await statisticsRepository.GetAsOfAsync(fixtureId, providerCode, At(18), default));
        Assert.Equal(snapshot.Id, (await statisticsRepository.GetAsOfAsync(fixtureId, providerCode, At(20), default))?.Id);

        var scheduleRepository = new DapperFixtureScheduleObservationRepository(factory);
        var schedule = new FixtureScheduleObservation(Guid.NewGuid(), fixtureId, providerCode, At(18), FixtureStatus.Finished, At(19), At(19), At(20), runId, string.Empty);
        schedule = schedule with { SourceFingerprint = HistoricalObservationFingerprint.ForSchedule(schedule) };
        Assert.True(await scheduleRepository.AddIfAbsentAsync(schedule, default));
        Assert.False(await scheduleRepository.AddIfAbsentAsync(schedule, default));

        var invalidForeignKey = schedule with { Id = Guid.NewGuid(), FixtureId = Guid.NewGuid(), SourceFingerprint = new string('A', 64) };
        await Assert.ThrowsAsync<SqlException>(() => scheduleRepository.AddIfAbsentAsync(invalidForeignKey, default));

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.FixtureStatisticsSnapshots WHERE Provider = @Provider;", new { Provider = providerCode }));
        Assert.Equal(2, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.TeamMatchStatistics t INNER JOIN dbo.FixtureStatisticsSnapshots s ON s.Id = t.FixtureStatisticsSnapshotId WHERE s.Provider = @Provider;", new { Provider = providerCode }));
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.FixtureScheduleObservations WHERE Provider = @Provider;", new { Provider = providerCode }));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("DELETE t FROM dbo.TeamMatchStatistics t INNER JOIN dbo.FixtureStatisticsSnapshots s ON s.Id = t.FixtureStatisticsSnapshotId WHERE s.Provider = @Provider; DELETE FROM dbo.FixtureScheduleObservations WHERE Provider = @Provider; DELETE FROM dbo.FixtureStatisticsSnapshots WHERE Provider = @Provider;", new { Provider = providerCode });
        var mappings = (await connection.QueryAsync<(Guid InternalEntityId, byte EntityType)>("SELECT InternalEntityId, EntityType FROM dbo.ProviderIdentifiers WHERE Provider = @Provider;", new { Provider = providerCode })).ToList();
        foreach (var id in mappings.Where(mapping => mapping.EntityType == 4).Select(mapping => mapping.InternalEntityId)) await connection.ExecuteAsync("DELETE FROM dbo.Fixtures WHERE Id = @Id;", new { Id = id });
        foreach (var id in mappings.Where(mapping => mapping.EntityType == 1).Select(mapping => mapping.InternalEntityId)) await connection.ExecuteAsync("DELETE FROM dbo.Seasons WHERE Id = @Id;", new { Id = id });
        foreach (var id in mappings.Where(mapping => mapping.EntityType == 0).Select(mapping => mapping.InternalEntityId)) await connection.ExecuteAsync("DELETE FROM dbo.Competitions WHERE Id = @Id;", new { Id = id });
        foreach (var id in mappings.Where(mapping => mapping.EntityType == 2).Select(mapping => mapping.InternalEntityId)) await connection.ExecuteAsync("DELETE FROM dbo.Teams WHERE Id = @Id;", new { Id = id });
        await connection.ExecuteAsync("DELETE FROM dbo.ProviderIdentifiers WHERE Provider = @Provider; DELETE FROM dbo.IngestionRuns WHERE Provider = @Provider;", new { Provider = providerCode });
    }

    private static DateTimeOffset At(int hour) => new(2026, 9, 20, hour, 0, 0, TimeSpan.Zero);

    private static string LoadConnectionString()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "FootballAnalytics.DatabaseMigrator", "appsettings.json")));
        return document.RootElement.GetProperty("ConnectionStrings").GetProperty("FootballAnalyticsDb").GetString() ?? throw new InvalidOperationException("FootballAnalyticsDb is missing.");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "FootballAnalytics.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the FootballAnalytics repository root.");
    }
}
