using FootballAnalytics.DatabaseMigrator;
using Microsoft.Extensions.Configuration;

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
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Migration failed: {exception.Message}");
    Environment.ExitCode = 1;
}
