using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FraudEngine.Tests;

public class FraudEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public FraudEndpointTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task BatchOverLimit_IsRejected()
    {
        var batch = Enumerable.Range(1, 101).Select(index => new
        {
            transactionId = $"TX-{index}", customerId = "CUS-1", amount = 1m, currency = "TRY",
            merchantCategory = "5411", countryCode = "TR", customerHomeCountry = "TR",
            occurredAt = "2026-07-20T12:00:00Z", transactionsLastHour = 0, isNewDevice = false
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/fraud/assess/batch", batch)).StatusCode);
    }

    [Fact]
    public async Task Health_UsesSecurityHeaders()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }
}
