using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FraudEngine.Api.Infrastructure;

/// <summary>
/// Durable storage for velocity counters and the assessment audit trail. Backed by SQLite so
/// data survives process restarts, unlike the in-memory dictionaries this replaced.
/// </summary>
public sealed class FraudDbContext(DbContextOptions<FraudDbContext> options) : DbContext(options)
{
    public DbSet<VelocityEventEntity> VelocityEvents => Set<VelocityEventEntity>();

    public DbSet<AssessmentAuditRecord> AuditRecords => Set<AssessmentAuditRecord>();

    public DbSet<RuleHitRecord> RuleHits => Set<RuleHitRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SQLite has no native DateTimeOffset type, so range comparisons on it cannot be
        // translated to SQL. Timestamps are normalized to UTC and stored as ticks instead.
        var utcTicksConverter = new ValueConverter<DateTimeOffset, long>(
            dateTimeOffset => dateTimeOffset.UtcTicks,
            ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

        modelBuilder.Entity<VelocityEventEntity>(entity =>
        {
            entity.Property(e => e.OccurredAt).HasConversion(utcTicksConverter);
            entity.HasIndex(e => new { e.CustomerId, e.OccurredAt });
        });

        modelBuilder.Entity<AssessmentAuditRecord>(entity =>
        {
            entity.Property(e => e.OccurredAt).HasConversion(utcTicksConverter);
            entity.Property(e => e.AssessedAt).HasConversion(utcTicksConverter);
            entity.HasIndex(e => e.TransactionId);
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.AssessedAt);
            entity.HasMany(e => e.Hits)
                .WithOne()
                .HasForeignKey(h => h.AssessmentAuditRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
