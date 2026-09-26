using Dapper;
using FootballAnalytics.Infrastructure.Explorer;
using FootballAnalytics.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace FootballAnalytics.IntegrationTests;

public sealed class ExplorerRepositorySqlTests
{
    // Opt in with a server name, never an application database connection string.
    public sealed class IsolatedSqlFactAttribute : FactAttribute
    {
        public IsolatedSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EXPLORER_TEST_SQL_SERVER")))
                Skip = "Set EXPLORER_TEST_SQL_SERVER to run against a disposable database.";
        }
    }

    [IsolatedSqlFact]
    public async Task Counts_pages_and_details_exclude_mismatched_membership_and_preserve_snapshot_mapping()
    {
        var database = "FootballAnalytics_ExplorerTest_" + Guid.NewGuid().ToString("N");
        var settings = new SqlConnectionStringBuilder
        {
            DataSource = Environment.GetEnvironmentVariable("EXPLORER_TEST_SQL_SERVER")!,
            InitialCatalog = "master", IntegratedSecurity = true,
            TrustServerCertificate = true, Pooling = false, ConnectTimeout = 5
        };
        await using var admin = new SqlConnection(settings.ConnectionString);
        await admin.OpenAsync();
        await admin.ExecuteAsync($"CREATE DATABASE [{database}]");
        try
        {
            settings.InitialCatalog = database;
            await using var connection = new SqlConnection(settings.ConnectionString);
            await connection.OpenAsync();
            // Purpose-built query fixture, not a migration. Independent foreign keys intentionally
            // allow the inconsistent membership that the production read queries must reject.
            await connection.ExecuteAsync("""
                CREATE TABLE dbo.Competitions (Id uniqueidentifier PRIMARY KEY, Name nvarchar(200) NOT NULL, Country nvarchar(100) NULL);
                CREATE TABLE dbo.Seasons (Id uniqueidentifier PRIMARY KEY, CompetitionId uniqueidentifier NOT NULL REFERENCES dbo.Competitions(Id), Name nvarchar(100) NOT NULL, StartDate date NOT NULL);
                CREATE TABLE dbo.Teams (Id uniqueidentifier PRIMARY KEY, Name nvarchar(200) NOT NULL);
                CREATE TABLE dbo.Fixtures (Id uniqueidentifier PRIMARY KEY, CompetitionId uniqueidentifier NOT NULL REFERENCES dbo.Competitions(Id), SeasonId uniqueidentifier NOT NULL REFERENCES dbo.Seasons(Id), HomeTeamId uniqueidentifier NOT NULL REFERENCES dbo.Teams(Id), AwayTeamId uniqueidentifier NOT NULL REFERENCES dbo.Teams(Id), KickoffUtc datetime2 NOT NULL, Status tinyint NOT NULL, HomeScore int NULL, AwayScore int NULL);
                CREATE TABLE dbo.FixtureStatisticsSnapshots (Id uniqueidentifier PRIMARY KEY, FixtureId uniqueidentifier NOT NULL REFERENCES dbo.Fixtures(Id), Provider nvarchar(100) NOT NULL, AvailableAtUtc datetime2 NOT NULL, IngestedAtUtc datetime2 NOT NULL);
                CREATE TABLE dbo.TeamMatchStatistics (FixtureStatisticsSnapshotId uniqueidentifier NOT NULL REFERENCES dbo.FixtureStatisticsSnapshots(Id), TeamId uniqueidentifier NOT NULL REFERENCES dbo.Teams(Id), Goals int NULL, Shots int NULL, ShotsOnTarget int NULL, PossessionPercentage decimal(5,2) NULL, Corners int NULL, Offsides int NULL, Fouls int NULL, YellowCards int NULL, RedCards int NULL);
                """);
            var competition = Guid.NewGuid(); var other = Guid.NewGuid(); var season = Guid.NewGuid();
            var home = Guid.NewGuid(); var away = Guid.NewGuid();
            var first = Guid.NewGuid(); var second = Guid.NewGuid(); var mismatch = Guid.NewGuid();
            var oldSnapshot = Guid.NewGuid(); var latestSnapshot = Guid.NewGuid(); var invalidSnapshot = Guid.NewGuid();
            await connection.ExecuteAsync("""
                INSERT dbo.Competitions VALUES (@competition, N'Competition A', NULL), (@other, N'Competition B', NULL);
                INSERT dbo.Seasons VALUES (@season, @competition, N'2020', '20200101');
                INSERT dbo.Teams VALUES (@home, N'Home'), (@away, N'Away');
                INSERT dbo.Fixtures VALUES
                    (@first, @competition, @season, @home, @away, '20200102', 2, NULL, 0),
                    (@second, @competition, @season, @home, @away, '20200101', 2, 1, 0),
                    (@mismatch, @other, @season, @home, @away, '20200103', 2, 9, 9);
                INSERT dbo.FixtureStatisticsSnapshots VALUES
                    (@oldSnapshot, @first, N'test-provider', '20200201', '20200201'),
                    (@latestSnapshot, @first, N'test-provider', '20200202', '20200203'),
                    (@invalidSnapshot, @mismatch, N'test-provider', '20200201', '20200201');
                INSERT dbo.TeamMatchStatistics (FixtureStatisticsSnapshotId, TeamId, Goals, Shots, Corners, PossessionPercentage) VALUES
                    (@oldSnapshot, @home, 99, 99, 99, 99),
                    (@latestSnapshot, @home, 0, NULL, 0, 42.50),
                    (@latestSnapshot, @away, 1, 3, NULL, NULL);
                """, new { competition, other, season, home, away, first, second, mismatch, oldSnapshot, latestSnapshot, invalidSnapshot });
            var repository = new DapperExplorerRepository(new SqlConnectionFactory(settings.ConnectionString));
            var catalogue = Assert.Single(await repository.GetSeasonsAsync(default));
            Assert.Equal(competition, catalogue.CompetitionId);
            Assert.Equal(2, catalogue.MatchCount);
            Assert.Equal(1, catalogue.StatisticsMatchCount);
            Assert.Equal(first, Assert.Single(await repository.GetMatchesAsync(season, 0, 1, default)).Id);
            Assert.Equal(second, Assert.Single(await repository.GetMatchesAsync(season, 1, 1, default)).Id);
            Assert.Empty(await repository.GetMatchesAsync(season, 2, 1, default));
            Assert.Null(await repository.GetMatchAsync(mismatch, default));
            var detail = Assert.IsType<FootballAnalytics.Application.Explorer.MatchDetail>(await repository.GetMatchAsync(first, default));
            Assert.Null(detail.Match.HomeScore);
            Assert.Equal(0, detail.Match.AwayScore);
            Assert.Equal(TimeSpan.Zero, detail.Match.KickoffUtc.Offset);
            var snapshot = Assert.Single(detail.Snapshots);
            Assert.Equal(latestSnapshot, snapshot.Id);
            Assert.Equal("test-provider", snapshot.Provider);
            Assert.Equal(new DateTimeOffset(2020, 2, 2, 0, 0, 0, TimeSpan.Zero), snapshot.AvailableAtUtc);
            Assert.Equal(new DateTimeOffset(2020, 2, 3, 0, 0, 0, TimeSpan.Zero), snapshot.IngestedAtUtc);
            var homeStats = Assert.Single(snapshot.Teams, x => x.IsHome);
            Assert.Null(homeStats.Shots);
            Assert.Equal(0, homeStats.Corners);
            Assert.Equal(42.50m, homeStats.PossessionPercentage);
            Assert.Equal(3, Assert.Single(snapshot.Teams, x => !x.IsHome).Shots);
            Assert.Empty((await repository.GetMatchAsync(second, default))!.Snapshots);
            // The inconsistent fixture is excluded from reads, not deleted or repaired.
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Fixtures WHERE Id = @mismatch", new { mismatch }));
        }
        finally
        {
            // Only this generated database can be dropped; no database name is accepted as input.
            await admin.ExecuteAsync($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]");
        }
    }
}
