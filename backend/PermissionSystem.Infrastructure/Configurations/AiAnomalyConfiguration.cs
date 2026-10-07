using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class AiAnomalyRuleConfiguration : IEntityTypeConfiguration<AiAnomalyRule>
{
    public void Configure(EntityTypeBuilder<AiAnomalyRule> b)
    {
        b.ToTable("ai_anomaly_rule", t => t.HasCheckConstraint("CK_ai_anomaly_rule_Contract", "[ContractVersion] = 1 AND [RuleType] = N'DemoPendingCount' AND [EpisodeSequence] >= 0"));
        b.ConfigureBaseEntity(); b.Property(r => r.RuleType).HasMaxLength(64).IsRequired();
        b.HasAlternateKey(r => new { r.TenantId, r.Id });
        b.HasIndex(r => new { r.TenantId, r.OwnerUserId }).IsUnique();
        b.HasIndex(r => new { r.TenantId, r.ScheduledTaskId }).IsUnique();
        b.HasOne<ScheduledTask>().WithMany().HasForeignKey(r => new { r.TenantId, r.ScheduledTaskId })
            .HasPrincipalKey(t => new { t.TenantId, t.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class AiAnomalyEventConfiguration : IEntityTypeConfiguration<AiAnomalyEvent>
{
    public void Configure(EntityTypeBuilder<AiAnomalyEvent> b)
    {
        b.ToTable("ai_anomaly_event", t => t.HasCheckConstraint("CK_ai_anomaly_event_Observation", "[ObservedCount] >= 1 AND [EpisodeSequence] >= 1 AND [AttemptCount] BETWEEN 0 AND 3"));
        b.ConfigureBaseEntity(); b.Property(e => e.ScopeFingerprint).HasMaxLength(64).IsRequired();
        b.Property(e => e.DeliveryKey).HasMaxLength(64).IsRequired();
        b.Property(e => e.DeliveryStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(e => e.CloseReason).HasMaxLength(64); b.Property(e => e.ErrorCode).HasMaxLength(64);
        b.Property(e => e.MessageId).HasMaxLength(64);
        b.HasIndex(e => new { e.TenantId, e.RuleId, e.EpisodeSequence }).IsUnique();
        b.HasIndex(e => new { e.TenantId, e.DeliveryKey }).IsUnique();
        b.HasIndex(e => new { e.TenantId, e.RecipientUserId, e.ObservedAt });
        b.HasOne<AiAnomalyRule>().WithMany().HasForeignKey(e => new { e.TenantId, e.RuleId })
            .HasPrincipalKey(r => new { r.TenantId, r.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
