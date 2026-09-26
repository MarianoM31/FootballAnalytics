using System.Data.Common;
using FootballAnalytics.Application.Explorer;

namespace FootballAnalytics.Api;

public static class ExplorerEndpoints
{
    public static void MapExplorer(this WebApplication app)
    {
        var group = app.MapGroup("/api/explorer");
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (ArgumentException exception)
            {
                return Results.Problem(statusCode: 400, title: exception.Message);
            }
            catch (DbException)
            {
                return Results.Problem(statusCode: 503, title: "Los datos no están disponibles. Inténtalo de nuevo más tarde.");
            }
        });
        group.MapGet("/competitions", (ExplorerService service, CancellationToken ct) => service.GetSeasonsAsync(ct));
        group.MapGet("/matches", (Guid seasonId, int? page, ExplorerService service, CancellationToken ct) =>
            service.GetMatchesAsync(seasonId, page ?? 1, ct));
        group.MapGet("/matches/{id:guid}", async (Guid id, ExplorerService service, CancellationToken ct) =>
            await service.GetMatchAsync(id, ct) is { } match ? Results.Ok(match) : Results.NotFound());
    }
}
