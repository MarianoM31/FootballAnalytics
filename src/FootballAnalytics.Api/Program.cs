using FootballAnalytics.Api;
using FootballAnalytics.Application.Explorer;
using FootballAnalytics.Application.Persistence;
using FootballAnalytics.Infrastructure.Explorer;
using FootballAnalytics.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(
    builder.Configuration.GetConnectionString("FootballAnalyticsDb")
        ?? throw new InvalidOperationException("FootballAnalyticsDb is required.")));
builder.Services.AddScoped<IExplorerRepository, DapperExplorerRepository>();
builder.Services.AddScoped<ExplorerService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("HealthCheck");

app.MapExplorer();

app.Run();

public partial class Program;
