# Architecture
```mermaid
C4Context
  title TransactionFraudEngine context
  Person(switch, "Payment switch")
  System(engine, "TransactionFraudEngine", "Explainable rule scoring")
  Rel(switch, engine, "HTTPS assessments")
```
```mermaid
flowchart LR
  Request --> API --> Scoring
  Scoring --> Velocity["Rolling velocity window<br/>(VelocityEvents table)"]
  Scoring --> Audit["Assessment audit trail<br/>(AuditRecords + RuleHits tables)"]
  Scoring --> Decision
  Velocity --> DB[("SQLite via EF Core")]
  Audit --> DB
```
Velocity counters and the audit trail are persisted through EF Core to SQLite, so both survive a
process restart. Rule thresholds are configuration-driven (`appsettings.json`), not hardcoded.
This is a rule-based demonstrator, not an ML model.
