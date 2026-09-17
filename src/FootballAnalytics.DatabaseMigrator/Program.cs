using FootballAnalytics.DatabaseMigrator;
using Microsoft.Extensions.Configuration;

namespace FootballAnalytics.DatabaseMigrator;

public static class DatabaseMigratorProgram
{
    public static async Task<int> Main()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("FootballAnalyticsDb")
            ?? throw new InvalidOperationException("Connection string 'FootballAnalyticsDb' is required.");

        var migrator = new DatabaseMigratorService(
            connectionString,
            new MigrationCatalog(Path.Combine(AppContext.BaseDirectory, "migrations")));

        try
        {
            await migrator.RunAsync();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Migration failed: {exception.Message}");
            return 1;
        }
    }
}
