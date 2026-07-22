using FraudEngine.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FraudEngine.Api.Domain;

/// <summary>
/// Applies configurable rule-based fraud scoring and persists velocity counters and the
/// assessment audit trail through <see cref="FraudDbContext"/>. Scoped per request: every
/// assessment reads and writes durable state instead of an in-process cache.
/// </summary>
public sealed class FraudScoringService(FraudDbContext dbContext, IOptionsSnapshot<FraudScoringOptions> options)
{
    private FraudScoringOptions Options => options.Value;

    public int MaxBatchSize => Options.MaxBatchSize;

    public async Task<FraudAssessment> AssessAsync(TransactionInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        Validate(input);
        var rules = Options;

        var serverVelocity = await IncrementVelocityAsync(input.CustomerId, input.OccurredAt, rules, cancellationToken);
        var velocity = Math.Max(input.TransactionsLastHour ?? 0, serverVelocity);

        var hits = new List<RuleHit>();

        if (input.Amount >= rules.AmountHighThreshold)
            hits.Add(new RuleHit("AMT_HIGH", "High transaction amount", rules.AmountHighScore));
        else if (input.Amount >= rules.AmountElevatedThreshold)
            hits.Add(new RuleHit("AMT_ELEVATED", "Elevated transaction amount", rules.AmountElevatedScore));

        if (velocity >= rules.VelocityBurstThreshold)
            hits.Add(new RuleHit("VEL_BURST", "Velocity burst in the last hour", rules.VelocityBurstScore));
        else if (velocity >= rules.VelocityElevatedThreshold)
            hits.Add(new RuleHit("VEL_ELEVATED", "Elevated transaction velocity", rules.VelocityElevatedScore));

        var hour = input.OccurredAt.UtcDateTime.Hour;
        if (hour >= rules.NightStartHourUtc && hour < rules.NightEndHourUtc && input.Amount >= rules.NightLargeAmountThreshold)
            hits.Add(new RuleHit("NIGHT_LARGE", "Large night-time transaction", rules.NightLargeScore));

        if (!string.Equals(input.CountryCode, input.CustomerHomeCountry, StringComparison.OrdinalIgnoreCase))
            hits.Add(new RuleHit("GEO_MISMATCH", "Country differs from customer home country", rules.GeoMismatchScore));

        if (rules.HighRiskMerchantCategories.Contains(input.MerchantCategory))
            hits.Add(new RuleHit("MCC_RISK", "High-risk merchant category", rules.MccRiskScore));

        if (input.IsNewDevice && input.Amount >= rules.NewDeviceAmountThreshold)
            hits.Add(new RuleHit("NEW_DEVICE_LARGE", "Large amount on a new device", rules.NewDeviceLargeScore));

        var score = Math.Min(rules.MaxScore, hits.Sum(h => h.Score));
        var decision = score switch
        {
            var s when s >= rules.BlockThreshold => RiskDecision.Block,
            var s when s >= rules.ReviewThreshold => RiskDecision.Review,
            _ => RiskDecision.Allow
        };

        var assessment = new FraudAssessment(input.TransactionId, score, decision, hits);

        dbContext.AuditRecords.Add(new AssessmentAuditRecord
        {
            TransactionId = input.TransactionId,
            CustomerId = input.CustomerId,
            Amount = input.Amount,
            Currency = input.Currency,
            MerchantCategory = input.MerchantCategory,
            CountryCode = input.CountryCode,
            CustomerHomeCountry = input.CustomerHomeCountry,
            OccurredAt = input.OccurredAt,
            TransactionsLastHourInput = input.TransactionsLastHour,
            IsNewDevice = input.IsNewDevice,
            RiskScore = score,
            Decision = decision,
            AssessedAt = DateTimeOffset.UtcNow,
            Hits = hits.Select(h => new RuleHitRecord
            {
                RuleCode = h.RuleCode,
                Description = h.Description,
                Score = h.Score
            }).ToList()
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return assessment;
    }

    public async Task<IReadOnlyList<AuditRecordResponse>> GetAuditAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.AuditRecords
            .Include(r => r.Hits)
            .OrderByDescending(r => r.AssessedAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);

        return records.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<AuditRecordResponse>> GetAuditByTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.AuditRecords
            .Include(r => r.Hits)
            .Where(r => r.TransactionId == transactionId)
            .OrderByDescending(r => r.AssessedAt)
            .ToListAsync(cancellationToken);

        return records.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<AuditRecordResponse>> GetAuditByCustomerAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.AuditRecords
            .Include(r => r.Hits)
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.AssessedAt)
            .ToListAsync(cancellationToken);

        return records.Select(ToResponse).ToList();
    }

    private async Task<int> IncrementVelocityAsync(string customerId, DateTimeOffset occurredAt, FraudScoringOptions rules, CancellationToken cancellationToken)
    {
        var cutoff = occurredAt - TimeSpan.FromHours(rules.VelocityWindowHours);

        var priorCount = await dbContext.VelocityEvents.CountAsync(
            e => e.CustomerId == customerId && e.OccurredAt >= cutoff && e.OccurredAt <= occurredAt,
            cancellationToken);

        dbContext.VelocityEvents.Add(new VelocityEventEntity
        {
            CustomerId = customerId,
            OccurredAt = occurredAt
        });

        return priorCount + 1;
    }

    private static AuditRecordResponse ToResponse(AssessmentAuditRecord record) => new(
        record.Id,
        record.TransactionId,
        record.CustomerId,
        record.Amount,
        record.Currency,
        record.MerchantCategory,
        record.CountryCode,
        record.CustomerHomeCountry,
        record.OccurredAt,
        record.TransactionsLastHourInput,
        record.IsNewDevice,
        record.RiskScore,
        record.Decision,
        record.AssessedAt,
        record.Hits.Select(h => new RuleHit(h.RuleCode, h.Description, h.Score)).ToList());

    private static void Validate(TransactionInput input)
    {
        if (input.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(input.Amount), "Amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(input.TransactionId) || string.IsNullOrWhiteSpace(input.CustomerId)
            || string.IsNullOrWhiteSpace(input.Currency) || string.IsNullOrWhiteSpace(input.MerchantCategory)
            || string.IsNullOrWhiteSpace(input.CountryCode) || string.IsNullOrWhiteSpace(input.CustomerHomeCountry))
            throw new ArgumentException("Transaction ID, customer ID, currency, merchant category, and country codes are required.");
        if (input.TransactionsLastHour is < 0)
            throw new ArgumentOutOfRangeException(nameof(input.TransactionsLastHour), "TransactionsLastHour cannot be negative.");
    }
}
