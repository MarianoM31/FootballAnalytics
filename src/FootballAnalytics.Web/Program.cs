var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("StatisticsApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5181/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

// Only explorer read routes are exposed. Web has no SQL dependency or connection string.
app.MapGet("/data/competitions", (HttpContext context, IHttpClientFactory clients) =>
    ForwardAsync("api/explorer/competitions", context, clients));
app.MapGet("/data/matches", (HttpContext context, IHttpClientFactory clients) =>
    ForwardAsync("api/explorer/matches" + context.Request.QueryString, context, clients));
app.MapGet("/data/matches/{id:guid}", (Guid id, HttpContext context, IHttpClientFactory clients) =>
    ForwardAsync($"api/explorer/matches/{id:D}", context, clients));
app.Run();

static async Task<IResult> ForwardAsync(string path, HttpContext context, IHttpClientFactory clients)
{
    try
    {
        using var response = await clients.CreateClient("StatisticsApi").GetAsync(path, context.RequestAborted);
        if (!response.IsSuccessStatusCode)
            return Results.Problem(statusCode: (int)response.StatusCode,
                title: "No se pudieron consultar los datos. Inténtalo de nuevo.");
        return Results.Content(await response.Content.ReadAsStringAsync(context.RequestAborted), "application/json");
    }
    catch (Exception exception) when (exception is HttpRequestException ||
        exception is OperationCanceledException && !context.RequestAborted.IsCancellationRequested)
    {
        return Results.Problem(statusCode: 503, title: "No se pudo conectar con el servicio de datos.");
    }
}
