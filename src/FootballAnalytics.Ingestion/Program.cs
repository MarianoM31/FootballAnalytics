using FootballAnalytics.Application.Ingestion;
using FootballAnalytics.Application.Persistence;
using FootballAnalytics.Infrastructure.FootballData;
using FootballAnalytics.Infrastructure.Ingestion;
using FootballAnalytics.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FootballAnalytics.Ingestion;

public static class IngestionProgram
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 2 || !string.Equals(args[0], "sync-fixtures", StringComparison.OrdinalIgnoreCase) || !string.Equals(args[1], "PL", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/FootballAnalytics.Ingestion -- sync-fixtures PL");
            return 1;
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();
        var token = configuration["FootballData:ApiToken"];
        if (string.IsNullOrWhiteSpace(token))
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
        services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(connectionString));
        services.AddTransient<ICompetitionRepository, DapperCompetitionRepository>();
        services.AddTransient<ISeasonRepository, DapperSeasonRepository>();
        services.AddTransient<ITeamRepository, DapperTeamRepository>();
        services.AddTransient<IFixtureRepository, DapperFixtureRepository>();
        services.AddTransient<IIngestionRunRepository, DapperIngestionRunRepository>();
        services.AddTransient<FixtureSyncService>();

        await using var provider = services.BuildServiceProvider();
        var footballDataProvider = provider.GetRequiredService<FootballDataProvider>();
        var syncService = provider.GetRequiredService<FixtureSyncService>();
        var fromUtc = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var toUtc = fromUtc.AddDays(14);

        try
        {
            var result = await syncService.SyncAsync(footballDataProvider, fromUtc, toUtc);
            Console.WriteLine($"Sync succeeded: {result.CompetitionsProcessed} competition(s), {result.SeasonsProcessed} season(s), {result.TeamsProcessed} team(s), {result.FixturesProcessed} fixture(s).");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Sync failed: {exception.Message}");
            return 1;
        }
    }
}
