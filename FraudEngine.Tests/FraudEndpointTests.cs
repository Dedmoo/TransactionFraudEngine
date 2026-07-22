using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FraudEngine.Api.Domain;
using Microsoft.Data.Sqlite;

namespace FraudEngine.Tests;

/// <summary>
/// Integration tests against the full HTTP pipeline. Each test gets its own temp SQLite file so
/// tests never share state, and it is cleaned up in <see cref="Dispose"/>.
/// </summary>
public sealed class FraudEndpointTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly List<string> _dbFiles = [];

    private string NewDbPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fraud-engine-api-{Guid.NewGuid():N}.db");
        _dbFiles.Add(path);
        return path;
    }

    private static object SampleTransaction(string transactionId, decimal amount = 1m) => new
    {
        transactionId,
        customerId = "CUS-1",
        amount,
        currency = "TRY",
        merchantCategory = "5411",
        countryCode = "TR",
        customerHomeCountry = "TR",
        occurredAt = "2026-07-20T12:00:00Z",
        transactionsLastHour = 0,
        isNewDevice = false
    };

    [Fact]
    public async Task BatchOverLimit_IsRejected()
    {
        using var factory = new FraudApiFactory(NewDbPath());
        using var client = factory.CreateClient();

        var batch = Enumerable.Range(1, 101).Select(index => SampleTransaction($"TX-{index}"));
        var response = await client.PostAsJsonAsync("/api/fraud/assess/batch", batch);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Health_UsesSecurityHeaders()
    {
        using var factory = new FraudApiFactory(NewDbPath());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Assess_PersistsAuditRecord_AcrossNewFactoryInstance()
    {
        var dbPath = NewDbPath();
        var transactionId = $"TX-{Guid.NewGuid():N}";

        using (var firstRun = new FraudApiFactory(dbPath))
        using (var client = firstRun.CreateClient())
        {
            var response = await client.PostAsJsonAsync("/api/fraud/assess", SampleTransaction(transactionId, amount: 27000m));
            response.EnsureSuccessStatusCode();
        }

        using var restarted = new FraudApiFactory(dbPath);
        using var restartedClient = restarted.CreateClient();

        var history = await restartedClient.GetFromJsonAsync<List<AuditRecordResponse>>(
            $"/api/fraud/audit/transaction/{transactionId}", JsonOptions);

        Assert.NotNull(history);
        var record = Assert.Single(history!);
        Assert.Equal(transactionId, record.TransactionId);
        Assert.Contains(record.Hits, h => h.RuleCode == "AMT_HIGH");
    }

    [Fact]
    public async Task Assess_HistoryEndpoints_FilterByCustomerAndTransaction()
    {
        using var factory = new FraudApiFactory(NewDbPath());
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/fraud/assess", SampleTransaction("TX-A", amount: 50m));
        await client.PostAsJsonAsync("/api/fraud/assess", SampleTransaction("TX-B", amount: 60m));

        var byTransaction = await client.GetFromJsonAsync<List<AuditRecordResponse>>(
            "/api/fraud/audit/transaction/TX-A", JsonOptions);
        Assert.Single(byTransaction!);

        var byCustomer = await client.GetFromJsonAsync<List<AuditRecordResponse>>(
            "/api/fraud/audit/customer/CUS-1", JsonOptions);
        Assert.Equal(2, byCustomer!.Count);

        var all = await client.GetFromJsonAsync<List<AuditRecordResponse>>("/api/fraud/audit", JsonOptions);
        Assert.Equal(2, all!.Count);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in _dbFiles.Where(File.Exists))
            File.Delete(path);
    }
}
