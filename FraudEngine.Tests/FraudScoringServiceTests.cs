using FraudEngine.Api.Domain;
using FraudEngine.Api.Infrastructure;
using FraudEngine.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace FraudEngine.Tests;

public sealed class FraudScoringServiceTests : IDisposable
{
    private readonly TempSqliteDatabase _database = new();
    private readonly FraudDbContext _dbContext;
    private readonly FraudScoringService _service;

    public FraudScoringServiceTests()
    {
        _dbContext = _database.CreateContext();
        _service = CreateService(_dbContext, new FraudScoringOptions());
    }

    private static FraudScoringService CreateService(FraudDbContext dbContext, FraudScoringOptions options) =>
        new(dbContext, new StaticOptionsSnapshot<FraudScoringOptions>(options));

    private static TransactionInput Base(
        decimal amount = 100m,
        int velocity = 1,
        string country = "TR",
        string home = "TR",
        string mcc = "5411",
        bool newDevice = false,
        int hourUtc = 12) =>
        new(
            TransactionId: "TX-1",
            CustomerId: "CUS-1",
            Amount: amount,
            Currency: "TRY",
            MerchantCategory: mcc,
            CountryCode: country,
            CustomerHomeCountry: home,
            OccurredAt: new DateTimeOffset(2026, 7, 20, hourUtc, 0, 0, TimeSpan.Zero),
            TransactionsLastHour: velocity,
            IsNewDevice: newDevice);

    [Fact]
    public async Task Assess_LowRisk_Allows()
    {
        var result = await _service.AssessAsync(Base());
        Assert.Equal(RiskDecision.Allow, result.Decision);
        Assert.True(result.RiskScore < 40);
    }

    [Fact]
    public async Task Assess_HighAmountAndGeoMismatch_BlocksOrReviews()
    {
        var result = await _service.AssessAsync(Base(amount: 30000m, country: "RU", home: "TR"));
        Assert.True(result.RiskScore >= 40);
        Assert.Contains(result.Hits, h => h.RuleCode == "AMT_HIGH");
        Assert.Contains(result.Hits, h => h.RuleCode == "GEO_MISMATCH");
    }

    [Fact]
    public async Task Assess_VelocityBurst_AddsScore()
    {
        var result = await _service.AssessAsync(Base(velocity: 9));
        Assert.Contains(result.Hits, h => h.RuleCode == "VEL_BURST");
        Assert.True(result.RiskScore >= 35);
    }

    [Fact]
    public async Task Assess_NightLarge_TriggersRule()
    {
        var result = await _service.AssessAsync(Base(amount: 4000m, hourUtc: 2));
        Assert.Contains(result.Hits, h => h.RuleCode == "NIGHT_LARGE");
    }

    [Fact]
    public async Task Assess_HighRiskMcc_TriggersRule()
    {
        var result = await _service.AssessAsync(Base(mcc: "7995"));
        Assert.Contains(result.Hits, h => h.RuleCode == "MCC_RISK");
    }

    [Fact]
    public async Task Assess_NewDeviceLarge_TriggersRule()
    {
        var result = await _service.AssessAsync(Base(amount: 6000m, newDevice: true));
        Assert.Contains(result.Hits, h => h.RuleCode == "NEW_DEVICE_LARGE");
    }

    [Fact]
    public async Task Assess_CombinedSignals_CanBlock()
    {
        var result = await _service.AssessAsync(Base(
            amount: 26000m,
            velocity: 8,
            country: "NG",
            home: "TR",
            mcc: "4829",
            newDevice: true,
            hourUtc: 1));

        Assert.Equal(RiskDecision.Block, result.Decision);
        Assert.Equal(100, result.RiskScore);
    }

    [Fact]
    public async Task Assess_ZeroAmount_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.AssessAsync(Base(amount: 0)));
    }

    [Fact]
    public async Task Assess_AppendsAuditEntry()
    {
        await _service.AssessAsync(Base());
        var audit = await _service.GetAuditAsync(skip: 0, take: 50);
        Assert.Single(audit);
        Assert.Equal("TX-1", audit[0].TransactionId);
    }

    [Fact]
    public async Task Assess_TracksVelocityWhenClientDoesNotSupplyIt()
    {
        for (var index = 0; index < 8; index++)
            await _service.AssessAsync(Base(velocity: 0) with { TransactionId = $"TX-{index}", TransactionsLastHour = null });
        var result = await _service.AssessAsync(Base(velocity: 0) with { TransactionId = "TX-final", TransactionsLastHour = null });
        Assert.Contains(result.Hits, hit => hit.RuleCode == "VEL_BURST");
    }

    [Fact]
    public async Task Assess_PersistsAcrossNewDbContextScope()
    {
        var assessed = await _service.AssessAsync(Base(amount: 12000m));

        using var freshContext = _database.CreateContext();
        var persisted = await freshContext.AuditRecords
            .Include(r => r.Hits)
            .SingleAsync(r => r.TransactionId == assessed.TransactionId);

        Assert.Equal(assessed.RiskScore, persisted.RiskScore);
        Assert.Equal(assessed.Decision, persisted.Decision);
        Assert.Equal(assessed.Hits.Count, persisted.Hits.Count);
        Assert.Contains(persisted.Hits, h => h.RuleCode == "AMT_ELEVATED");

        var velocityEventCount = await freshContext.VelocityEvents.CountAsync(e => e.CustomerId == "CUS-1");
        Assert.Equal(1, velocityEventCount);
    }

    [Fact]
    public async Task Assess_UsesConfiguredAmountThreshold_NotHardcoded()
    {
        var lenientOptions = new FraudScoringOptions { AmountHighThreshold = 500m, AmountHighScore = 40 };
        var lenientService = CreateService(_dbContext, lenientOptions);

        var result = await lenientService.AssessAsync(Base(amount: 600m));

        Assert.Contains(result.Hits, h => h.RuleCode == "AMT_HIGH");
    }

    [Fact]
    public async Task Assess_UsesConfiguredHighRiskMcc_NotHardcoded()
    {
        var customOptions = new FraudScoringOptions
        {
            HighRiskMerchantCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "1234" }
        };
        var customService = CreateService(_dbContext, customOptions);

        var flagged = await customService.AssessAsync(Base(mcc: "1234"));
        Assert.Contains(flagged.Hits, h => h.RuleCode == "MCC_RISK");

        var notFlagged = await customService.AssessAsync(Base(mcc: "7995") with { TransactionId = "TX-2" });
        Assert.DoesNotContain(notFlagged.Hits, h => h.RuleCode == "MCC_RISK");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _database.Dispose();
    }
}
