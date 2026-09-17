using Microsoft.AspNetCore.Mvc.Testing;

namespace FootballAnalytics.IntegrationTests;

public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Get_health_returns_healthy_status()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        Assert.Equal("{\"status\":\"healthy\"}", await response.Content.ReadAsStringAsync());
    }
}
