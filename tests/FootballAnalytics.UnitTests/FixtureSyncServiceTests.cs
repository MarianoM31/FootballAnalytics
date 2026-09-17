using FootballAnalytics.Application.Ingestion;
using FootballAnalytics.Domain.Enums;

namespace FootballAnalytics.UnitTests;

public sealed class FixtureSyncServiceTests
{
    [Fact]
    public async Task Sync_creates_mappings_and_is_idempotent()
    {
        var store = new InMemoryIngestionStore();
        var service = CreateService(store);
        var provider = new FakeFootballDataProvider();

        await service.SyncAsync(provider, FromUtc, ToUtc);
        await service.SyncAsync(provider, FromUtc, ToUtc);

        Assert.Single(store.Competitions);
        Assert.Single(store.Seasons);
        Assert.Equal(2, store.Teams.Count);
        Assert.Single(store.Fixtures);
        Assert.Equal(5, store.ProviderMappings.Count);
        Assert.Equal(2, store.SucceededRuns.Count);
    }

    [Fact]
    public async Task Sync_updates_an_existing_fixture_when_provider_changes_it()
    {
        var store = new InMemoryIngestionStore();
        var service = CreateService(store);
        var provider = new FakeFootballDataProvider();
        await service.SyncAsync(provider, FromUtc, ToUtc);

        provider.Fixture = provider.Fixture with
        {
            KickoffUtc = new DateTimeOffset(2026, 9, 21, 19, 0, 0, TimeSpan.Zero),
            Status = FixtureStatus.Finished,
            HomeScore = 2,
            AwayScore = 1
        };
        await service.SyncAsync(provider, FromUtc, ToUtc);

        var fixture = Assert.Single(store.Fixtures.Values);
        Assert.Equal(FixtureStatus.Finished, fixture.Status);
        Assert.Equal(2, fixture.HomeScore);
        Assert.Equal(1, fixture.AwayScore);
        Assert.Equal(provider.Fixture.KickoffUtc, fixture.KickoffUtc);
    }

    [Fact]
    public async Task Sync_records_a_failed_run_when_provider_throws()
    {
        var store = new InMemoryIngestionStore();
        var service = CreateService(store);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SyncAsync(new FailingFootballDataProvider(), FromUtc, ToUtc));

        var run = Assert.Single(store.FailedRuns);
        Assert.Equal("Provider is unavailable.", run.ErrorMessage);
        Assert.NotNull(run.CompletedAtUtc);
    }

    [Fact]
    public async Task Sync_rejects_non_utc_fixture_timestamps()
    {
        var store = new InMemoryIngestionStore();
        var service = CreateService(store);
        var provider = new FakeFootballDataProvider
        {
            Fixture = new FakeFootballDataProvider().Fixture with { KickoffUtc = new DateTimeOffset(2026, 9, 20, 18, 0, 0, TimeSpan.FromHours(2)) }
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.SyncAsync(provider, FromUtc, ToUtc));
        Assert.Single(store.FailedRuns);
    }

    private static readonly DateTimeOffset FromUtc = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ToUtc = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static FixtureSyncService CreateService(InMemoryIngestionStore store) =>
        new(store, store, store, store, store);

    private sealed class FakeFootballDataProvider : IFootballDataProvider
    {
        public string ProviderCode => "fake";
        public NormalizedFixture Fixture { get; set; } = new("fixture-1", "competition-1", "season-1", "team-arsenal", "team-liverpool", new DateTimeOffset(2026, 9, 20, 18, 0, 0, TimeSpan.Zero), FixtureStatus.Scheduled, null, null);

        public Task<IReadOnlyList<NormalizedCompetition>> GetCompetitionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NormalizedCompetition>>([new("competition-1", "Premier League", "England")]);
        public Task<IReadOnlyList<NormalizedSeason>> GetSeasonsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NormalizedSeason>>([new("season-1", "competition-1", "2026/27", new DateOnly(2026, 8, 1), new DateOnly(2027, 5, 31))]);
        public Task<IReadOnlyList<NormalizedTeam>> GetTeamsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NormalizedTeam>>([new("team-arsenal", "Arsenal"), new("team-liverpool", "Liverpool")]);
        public Task<IReadOnlyList<NormalizedFixture>> GetFixturesAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NormalizedFixture>>([Fixture]);
    }

    private sealed class FailingFootballDataProvider : IFootballDataProvider
    {
        public string ProviderCode => "fake";
        public Task<IReadOnlyList<NormalizedCompetition>> GetCompetitionsAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("Provider is unavailable.");
        public Task<IReadOnlyList<NormalizedSeason>> GetSeasonsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<NormalizedTeam>> GetTeamsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<NormalizedFixture>> GetFixturesAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class InMemoryIngestionStore : ICompetitionRepository, ISeasonRepository, ITeamRepository, IFixtureRepository, IIngestionRunRepository
    {
        private readonly Dictionary<(string Provider, string Type, string ExternalId), Guid> mappings = [];
        public Dictionary<Guid, NormalizedCompetition> Competitions { get; } = [];
        public Dictionary<Guid, NormalizedSeason> Seasons { get; } = [];
        public Dictionary<Guid, NormalizedTeam> Teams { get; } = [];
        public Dictionary<Guid, StoredFixture> Fixtures { get; } = [];
        public IReadOnlyDictionary<(string Provider, string Type, string ExternalId), Guid> ProviderMappings => mappings;
        public List<StoredRun> SucceededRuns { get; } = [];
        public List<StoredRun> FailedRuns { get; } = [];
        private readonly Dictionary<Guid, StoredRun> runs = [];

        public Task<Guid> UpsertAsync(string providerCode, NormalizedCompetition item, CancellationToken cancellationToken)
        {
            var id = Map(providerCode, "competition", item.ExternalId);
            Competitions[id] = item;
            return Task.FromResult(id);
        }

        public Task<Guid> UpsertAsync(string providerCode, NormalizedSeason item, Guid competitionId, CancellationToken cancellationToken)
        {
            var id = Map(providerCode, "season", item.ExternalId);
            Seasons[id] = item;
            return Task.FromResult(id);
        }

        public Task<Guid> UpsertAsync(string providerCode, NormalizedTeam item, CancellationToken cancellationToken)
        {
            var id = Map(providerCode, "team", item.ExternalId);
            Teams[id] = item;
            return Task.FromResult(id);
        }

        public Task<Guid> UpsertAsync(string providerCode, NormalizedFixture item, Guid competitionId, Guid seasonId, Guid homeTeamId, Guid awayTeamId, CancellationToken cancellationToken)
        {
            var id = Map(providerCode, "fixture", item.ExternalId);
            Fixtures[id] = new StoredFixture(item.KickoffUtc, item.Status, item.HomeScore, item.AwayScore);
            return Task.FromResult(id);
        }

        public Task<Guid> StartAsync(string providerCode, DateTimeOffset startedAtUtc, CancellationToken cancellationToken)
        {
            var id = Guid.NewGuid();
            runs[id] = new StoredRun(id, IngestionRunStatus.Running, null, null);
            return Task.FromResult(id);
        }

        public Task CompleteAsync(Guid id, IngestionResult result, DateTimeOffset completedAtUtc, CancellationToken cancellationToken)
        {
            var run = runs[id] with { Status = IngestionRunStatus.Succeeded, CompletedAtUtc = completedAtUtc };
            runs[id] = run;
            SucceededRuns.Add(run);
            return Task.CompletedTask;
        }

        public Task FailAsync(Guid id, string errorMessage, DateTimeOffset completedAtUtc, CancellationToken cancellationToken)
        {
            var run = runs[id] with { Status = IngestionRunStatus.Failed, CompletedAtUtc = completedAtUtc, ErrorMessage = errorMessage };
            runs[id] = run;
            FailedRuns.Add(run);
            return Task.CompletedTask;
        }

        private Guid Map(string provider, string type, string externalId)
        {
            var key = (provider, type, externalId);
            if (!mappings.TryGetValue(key, out var id)) { id = Guid.NewGuid(); mappings[key] = id; }
            return id;
        }
    }

    private sealed record StoredFixture(DateTimeOffset KickoffUtc, FixtureStatus Status, int? HomeScore, int? AwayScore);
    private sealed record StoredRun(Guid Id, IngestionRunStatus Status, DateTimeOffset? CompletedAtUtc, string? ErrorMessage);
}
