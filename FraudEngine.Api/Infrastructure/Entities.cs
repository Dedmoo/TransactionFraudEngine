using FraudEngine.Api.Domain;

namespace FraudEngine.Api.Infrastructure;

/// <summary>One transaction timestamp counted toward a customer's rolling velocity window.</summary>
public sealed class VelocityEventEntity
{
    public long Id { get; set; }
    public required string CustomerId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

/// <summary>Durable record of a single fraud assessment, including the input that produced it.</summary>
public sealed class AssessmentAuditRecord
{
    public long Id { get; set; }
    public required string TransactionId { get; set; }
    public required string CustomerId { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string MerchantCategory { get; set; }
    public required string CountryCode { get; set; }
    public required string CustomerHomeCountry { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public int? TransactionsLastHourInput { get; set; }
    public bool IsNewDevice { get; set; }
    public int RiskScore { get; set; }
    public RiskDecision Decision { get; set; }
    public DateTimeOffset AssessedAt { get; set; }

    public List<RuleHitRecord> Hits { get; set; } = [];
}

/// <summary>Rule hit belonging to a persisted <see cref="AssessmentAuditRecord"/>.</summary>
public sealed class RuleHitRecord
{
    public long Id { get; set; }
    public long AssessmentAuditRecordId { get; set; }
    public required string RuleCode { get; set; }
    public required string Description { get; set; }
    public int Score { get; set; }
}
