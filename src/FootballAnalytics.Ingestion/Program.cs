using FootballAnalytics.Application.Ingestion;
using FootballAnalytics.Application.Historical;
using FootballAnalytics.Application.Persistence;
using FootballAnalytics.Infrastructure.FootballData;
using FootballAnalytics.Infrastructure.Ingestion;
using FootballAnalytics.Infrastructure.Persistence;
using FootballAnalytics.Infrastructure.Historical;
using FootballAnalytics.Infrastructure.StatsBomb;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FootballAnalytics.Ingestion;

public static class IngestionProgram
{
    public static async Task<int> Main(string[] args)
    {
        var footballDataCommand = args.Length == 2 && string.Equals(args[0], "sync-fixtures", StringComparison.OrdinalIgnoreCase) && string.Equals(args[1], "PL", StringComparison.OrdinalIgnoreCase);
        var statsBombCommand = args.Length == 3 && string.Equals(args[0], "sync-statsbomb-history", StringComparison.OrdinalIgnoreCase);
        if (!footballDataCommand && !statsBombCommand)
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/FootballAnalytics.Ingestion -- sync-fixtures PL");
            Console.Error.WriteLine("   or: dotnet run --project src/FootballAnalytics.Ingestion -- sync-statsbomb-history <competitionId> <seasonId>");
            return 1;
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();
        var token = configuration["FootballData:ApiToken"];
        if (footballDataCommand && string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine("FootballData__ApiToken is not configured. Run:");
            Console.Error.WriteLine("[Environment]::SetEnvironmentVariable('FootballData__ApiToken', '<MI_API_TOKEN>', 'User')");
            return 1;
        }

        var connectionString = configuration.GetConnectionString("FootballAnalyticsDb")
            ?? throw new InvalidOperationException("Connection string 'FootballAnalyticsDb' is required.");
        var services = new ServiceCollection();
        services.Configure<FootballDataOptions>(configuration.GetSection(FootballDataOptions.SectionName));
        services.AddHttpClient<FootballDataProvider>();
        services.AddHttpClient<StatsBombOpenDataProvider>(client =>
        {
            client.BaseAddress = new Uri("https://raw.githubusercontent.com/hudl/open-data/master/data/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(connectionString));
        services.AddTransient<ICompetitionRepository, DapperCompetitionRepository>();
        services.AddTransient<ISeasonRepository, DapperSeasonRepository>();
        services.AddTransient<ITeamRepository, DapperTeamRepository>();
        services.AddTransient<IFixtureRepository, DapperFixtureRepository>();
        services.AddTransient<IIngestionRunRepository, DapperIngestionRunRepository>();
        services.AddTransient<IFixtureStatisticsSnapshotRepository, DapperFixtureStatisticsSnapshotRepository>();
        services.AddTransient<FixtureSyncService>();
        services.AddTransient<StatsBombEventStatisticsCalculator>();
        services.AddTransient<HistoricalStatisticsIngestionService>();

        await using var provider = services.BuildServiceProvider();
        try
        {
            if (footballDataCommand)
            {
                var footballDataProvider = provider.GetRequiredService<FootballDataProvider>();
                var syncService = provider.GetRequiredService<FixtureSyncService>();
                var fromUtc = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
                var result = await syncService.SyncAsync(footballDataProvider, fromUtc, fromUtc.AddDays(14));
                Console.WriteLine($"Sync succeeded: {result.CompetitionsProcessed} competition(s), {result.SeasonsProcessed} season(s), {result.TeamsProcessed} team(s), {result.FixturesProcessed} fixture(s).");
            }
            else
            {
                var statsBombProvider = provider.GetRequiredService<StatsBombOpenDataProvider>();
                var service = provider.GetRequiredService<HistoricalStatisticsIngestionService>();
                var result = await service.SyncAsync(statsBombProvider, args[1], args[2]);
                Console.WriteLine($"StatsBomb history succeeded: {result.CompetitionsProcessed} competition(s), {result.SeasonsProcessed} season(s), {result.TeamsProcessed} team(s), {result.FixturesProcessed} fixture(s), {result.StatisticsSnapshotsCreated} statistics snapshot(s) created.");
            }
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Sync failed: {exception.Message}");
            return 1;
        }
    }
}
