namespace FraudEngine.Api.Domain;

/// <summary>
/// Rule thresholds and scores for <see cref="FraudScoringService"/>, bound from the
/// "FraudScoring" section of appsettings.json. Nothing here is hardcoded in the service.
/// </summary>
public sealed class FraudScoringOptions
{
    public const string SectionName = "FraudScoring";

    public decimal AmountElevatedThreshold { get; set; } = 10_000m;
    public int AmountElevatedScore { get; set; } = 20;

    public decimal AmountHighThreshold { get; set; } = 25_000m;
    public int AmountHighScore { get; set; } = 40;

    public int VelocityElevatedThreshold { get; set; } = 4;
    public int VelocityElevatedScore { get; set; } = 15;

    public int VelocityBurstThreshold { get; set; } = 8;
    public int VelocityBurstScore { get; set; } = 35;

    public int VelocityWindowHours { get; set; } = 1;

    public int NightStartHourUtc { get; set; } = 0;
    public int NightEndHourUtc { get; set; } = 5;
    public decimal NightLargeAmountThreshold { get; set; } = 3_000m;
    public int NightLargeScore { get; set; } = 25;

    public int GeoMismatchScore { get; set; } = 30;

    public HashSet<string> HighRiskMerchantCategories { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "7995", // gambling
        "6051", // quasi cash
        "4829"  // money transfer
    };
    public int MccRiskScore { get; set; } = 25;

    public decimal NewDeviceAmountThreshold { get; set; } = 5_000m;
    public int NewDeviceLargeScore { get; set; } = 20;

    public int MaxScore { get; set; } = 100;
    public int ReviewThreshold { get; set; } = 40;
    public int BlockThreshold { get; set; } = 70;

    public int MaxBatchSize { get; set; } = 100;
}
