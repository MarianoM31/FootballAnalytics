using FootballAnalytics.DatabaseMigrator;

namespace FootballAnalytics.UnitTests;

public sealed class MigrationCatalogTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"football-analytics-tests-{Guid.NewGuid()}");

    [Fact]
    public void Discover_returns_migrations_ordered_by_identifier()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "002_second.sql"), "SELECT 2;");
        File.WriteAllText(Path.Combine(directory, "001_first.sql"), "SELECT 1;");

        var migrations = new MigrationCatalog(directory).Discover();

        Assert.Collection(migrations,
            migration => Assert.Equal("001", migration.Id),
            migration => Assert.Equal("002", migration.Id));
    }

    [Fact]
    public void Discover_rejects_an_invalid_migration_file_name()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "initial.sql"), "SELECT 1;");

        Assert.Throws<InvalidOperationException>(() => new MigrationCatalog(directory).Discover());
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
