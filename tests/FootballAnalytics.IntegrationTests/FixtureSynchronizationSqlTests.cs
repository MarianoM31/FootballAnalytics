using System.Text.Json;
using Dapper;
using FootballAnalytics.Application.Ingestion;
using FootballAnalytics.DatabaseMigrator;
using FootballAnalytics.Infrastructure.Ingestion;
using FootballAnalytics.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace FootballAnalytics.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class FixtureSynchronizationSqlTests : IAsyncLifetime
{
    private readonly string providerCode = $"phase1a-test-{Guid.NewGuid():N}";
    private readonly string connectionString = LoadConnectionString();

    [Fact]
    public async Task Sync_persists_idempotently_and_updates_existing_fixture()
    {
        var root = FindRepositoryRoot();
        var migrator = new DatabaseMigratorService(connectionString, new MigrationCatalog(Path.Combine(root, "database", "migrations")));
        await migrator.RunAsync();

        var factory = new SqlConnectionFactory(connectionString);
        var service = new FixtureSyncService(
            new DapperCompetitionRepository(factory),
            new DapperSeasonRepository(factory),
            new DapperTeamRepository(factory),
            new DapperFixtureRepository(factory),
            new DapperIngestionRunRepository(factory));
        var provider = new SqlTestFootballDataProvider(providerCode);

        await service.SyncAsync(provider, FromUtc, ToUtc);
        await service.SyncAsync(provider, FromUtc, ToUtc);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(5, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.ProviderIdentifiers WHERE Provider = @Provider;", new { Provider = providerCode }));
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Fixtures f INNER JOIN dbo.ProviderIdentifiers p ON p.InternalEntityId = f.Id WHERE p.Provider = @Provider AND p.EntityType = 4;", new { Provider = providerCode }));

        provider.Fixture = provider.Fixture with { Status = FootballAnalytics.Domain.Enums.FixtureStatus.Finished, HomeScore = 2, AwayScore = 1 };
        await service.SyncAsync(provider, FromUtc, ToUtc);

        var updated = await connection.QuerySingleAsync<(byte Status, int HomeScore, int AwayScore)>(
            "SELECT f.Status, f.HomeScore, f.AwayScore FROM dbo.Fixtures f INNER JOIN dbo.ProviderIdentifiers p ON p.InternalEntityId = f.Id WHERE p.Provider = @Provider AND p.EntityType = 4;",
            new { Provider = providerCode });
        Assert.Equal((byte)FootballAnalytics.Domain.Enums.FixtureStatus.Finished, updated.Status);
        Assert.Equal(2, updated.HomeScore);
        Assert.Equal(1, updated.AwayScore);
        Assert.Equal(3, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.IngestionRuns WHERE Provider = @Provider AND Status = 1;", new { Provider = providerCode }));
    }

    public async Task InitializeAsync() => await Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        var mappings = (await connection.QueryAsync<(Guid InternalEntityId, byte EntityType)>(
            "SELECT InternalEntityId, EntityType FROM dbo.ProviderIdentifiers WHERE Provider = @Provider;", new { Provider = providerCode })).ToList();
        foreach (var id in mappings.Where(mapping => mapping.EntityType == 4).Select(mapping => mapping.InternalEntityId))
            await connection.ExecuteAsync("DELETE FROM dbo.Fixtures WHERE Id = @Id;", new { Id = id });
        foreach (var id in mappings.Where(mapping => mapping.EntityType == 1).Select(mapping => mapping.InternalEntityId))
            await connection.ExecuteAsync("DELETE FROM dbo.Seasons WHERE Id = @Id;", new { Id = id });
        foreach (var id in mappings.Where(mapping => mapping.EntityType == 0).Select(mapping => mapping.InternalEntityId))
            await connection.ExecuteAsync("DELETE FROM dbo.Competitions WHERE Id = @Id;", new { Id = id });
        foreach (var id in mappings.Where(mapping => mapping.EntityType == 2).Select(mapping => mapping.InternalEntityId))
            await connection.ExecuteAsync("DELETE FROM dbo.Teams WHERE Id = @Id;", new { Id = id });
        await connection.ExecuteAsync("DELETE FROM dbo.ProviderIdentifiers WHERE Provider = @Provider; DELETE FROM dbo.IngestionRuns WHERE Provider = @Provider;", new { Provider = providerCode });
    }

    private static readonly DateTimeOffset FromUtc = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ToUtc = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static string LoadConnectionString()
    {
        var path = Path.Combine(FindRepositoryRoot(), "src", "FootballAnalytics.DatabaseMigrator", "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("ConnectionStrings").GetProperty("FootballAnalyticsDb").GetString()
            ?? throw new InvalidOperationException("FootballAnalyticsDb is missing from test configuration.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FootballAnalytics.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the FootballAnalytics repository root.");
    }

    private sealed class SqlTestFootballDataProvider(string providerCode) : IFootballDataProvider
    {
        public string ProviderCode { get; } = providerCode;
        public NormalizedFixture Fixture { get; set; } = new("fixture", "competition", "season", "arsenal", "liverpool", new DateTimeOffset(2026, 9, 20, 18, 0, 0, TimeSpan.Zero), FootballAnalytics.Domain.Enums.FixtureStatus.Scheduled, null, null);
        public Task<IReadOnlyList<NormalizedCompetition>> GetCompetitionsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NormalizedCompetition>>([new("competition", "Premier League", "England")]);
        public Task<IReadOnlyList<NormalizedSeason>> GetSeasonsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NormalizedSeason>>([new("season", "competition", "2026/27", new DateOnly(2026, 8, 1), new DateOnly(2027, 5, 31))]);
        public Task<IReadOnlyList<NormalizedTeam>> GetTeamsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NormalizedTeam>>([new("arsenal", "Arsenal"), new("liverpool", "Liverpool")]);
        public Task<IReadOnlyList<NormalizedFixture>> GetFixturesAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NormalizedFixture>>([Fixture]);
    }
}
