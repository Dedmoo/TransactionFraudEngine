namespace FraudEngine.Api.Domain;

public enum RiskDecision
{
    Allow = 0,
    Review = 1,
    Block = 2
}

public sealed record TransactionInput(
    string TransactionId,
    string CustomerId,
    decimal Amount,
    string Currency,
    string MerchantCategory,
    string CountryCode,
    string CustomerHomeCountry,
    DateTimeOffset OccurredAt,
    int TransactionsLastHour,
    bool IsNewDevice);

public sealed record RuleHit(string RuleCode, string Description, int Score);

public sealed record FraudAssessment(
    string TransactionId,
    int RiskScore,
    RiskDecision Decision,
    IReadOnlyList<RuleHit> Hits);
