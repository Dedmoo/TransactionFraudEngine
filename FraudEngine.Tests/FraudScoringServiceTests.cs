using FraudEngine.Api.Domain;

namespace FraudEngine.Tests;

public class FraudScoringServiceTests
{
    private readonly FraudScoringService _service = new();

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
    public void Assess_LowRisk_Allows()
    {
        var result = _service.Assess(Base());
        Assert.Equal(RiskDecision.Allow, result.Decision);
        Assert.True(result.RiskScore < 40);
    }

    [Fact]
    public void Assess_HighAmountAndGeoMismatch_BlocksOrReviews()
    {
        var result = _service.Assess(Base(amount: 30000m, country: "RU", home: "TR"));
        Assert.True(result.RiskScore >= 40);
        Assert.Contains(result.Hits, h => h.RuleCode == "AMT_HIGH");
        Assert.Contains(result.Hits, h => h.RuleCode == "GEO_MISMATCH");
    }

    [Fact]
    public void Assess_VelocityBurst_AddsScore()
    {
        var result = _service.Assess(Base(velocity: 9));
        Assert.Contains(result.Hits, h => h.RuleCode == "VEL_BURST");
        Assert.True(result.RiskScore >= 35);
    }

    [Fact]
    public void Assess_NightLarge_TriggersRule()
    {
        var result = _service.Assess(Base(amount: 4000m, hourUtc: 2));
        Assert.Contains(result.Hits, h => h.RuleCode == "NIGHT_LARGE");
    }

    [Fact]
    public void Assess_HighRiskMcc_TriggersRule()
    {
        var result = _service.Assess(Base(mcc: "7995"));
        Assert.Contains(result.Hits, h => h.RuleCode == "MCC_RISK");
    }

    [Fact]
    public void Assess_NewDeviceLarge_TriggersRule()
    {
        var result = _service.Assess(Base(amount: 6000m, newDevice: true));
        Assert.Contains(result.Hits, h => h.RuleCode == "NEW_DEVICE_LARGE");
    }

    [Fact]
    public void Assess_CombinedSignals_CanBlock()
    {
        var result = _service.Assess(Base(
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
}
