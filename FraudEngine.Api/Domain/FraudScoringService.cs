namespace FraudEngine.Api.Domain;

public sealed class FraudScoringService
{
    private static readonly TimeSpan VelocityWindow = TimeSpan.FromHours(1);
    private readonly object _gate = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> _velocityByCustomer = new(StringComparer.Ordinal);
    private readonly List<AssessmentAuditEntry> _audit = [];
    private static readonly HashSet<string> HighRiskMcc = new(StringComparer.OrdinalIgnoreCase)
    {
        "7995", // gambling
        "6051", // quasi cash
        "4829"  // money transfer
    };

    public FraudAssessment Assess(TransactionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Validate(input);
        var serverVelocity = IncrementVelocity(input.CustomerId, input.OccurredAt);
        var velocity = Math.Max(input.TransactionsLastHour ?? 0, serverVelocity);

        var hits = new List<RuleHit>();

        if (input.Amount >= 25000m)
            hits.Add(new RuleHit("AMT_HIGH", "High transaction amount", 40));
        else if (input.Amount >= 10000m)
            hits.Add(new RuleHit("AMT_ELEVATED", "Elevated transaction amount", 20));

        if (velocity >= 8)
            hits.Add(new RuleHit("VEL_BURST", "Velocity burst in the last hour", 35));
        else if (velocity >= 4)
            hits.Add(new RuleHit("VEL_ELEVATED", "Elevated transaction velocity", 15));

        var hour = input.OccurredAt.UtcDateTime.Hour;
        if (hour is >= 0 and < 5 && input.Amount >= 3000m)
            hits.Add(new RuleHit("NIGHT_LARGE", "Large night-time transaction", 25));

        if (!string.Equals(input.CountryCode, input.CustomerHomeCountry, StringComparison.OrdinalIgnoreCase))
            hits.Add(new RuleHit("GEO_MISMATCH", "Country differs from customer home country", 30));

        if (HighRiskMcc.Contains(input.MerchantCategory))
            hits.Add(new RuleHit("MCC_RISK", "High-risk merchant category", 25));

        if (input.IsNewDevice && input.Amount >= 5000m)
            hits.Add(new RuleHit("NEW_DEVICE_LARGE", "Large amount on a new device", 20));

        var score = Math.Min(100, hits.Sum(h => h.Score));
        var decision = score switch
        {
            >= 70 => RiskDecision.Block,
            >= 40 => RiskDecision.Review,
            _ => RiskDecision.Allow
        };

        var assessment = new FraudAssessment(input.TransactionId, score, decision, hits);
        lock (_gate)
            _audit.Add(new AssessmentAuditEntry(DateTimeOffset.UtcNow, input, assessment));
        return assessment;
    }

    public IReadOnlyList<AssessmentAuditEntry> GetAudit() {
        lock (_gate)
            return _audit.ToList();
    }

    private int IncrementVelocity(string customerId, DateTimeOffset occurredAt)
    {
        lock (_gate)
        {
            if (!_velocityByCustomer.TryGetValue(customerId, out var events))
                _velocityByCustomer[customerId] = events = new Queue<DateTimeOffset>();
            var cutoff = occurredAt - VelocityWindow;
            while (events.TryPeek(out var timestamp) && timestamp < cutoff)
                events.Dequeue();
            events.Enqueue(occurredAt);
            return events.Count;
        }
    }

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
