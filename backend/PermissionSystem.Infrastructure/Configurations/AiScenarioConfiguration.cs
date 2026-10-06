using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class AiScenarioConfiguration : IEntityTypeConfiguration<AiScenario>
{
    public void Configure(EntityTypeBuilder<AiScenario> b)
    {
        b.ToTable("ai_scenario"); b.ConfigureBaseEntity();
        b.Property(s => s.Code).HasMaxLength(100).IsRequired();
        b.Property(s => s.Name).HasMaxLength(100).IsRequired();
        b.Property(s => s.Description).HasMaxLength(1000).IsRequired();
        b.HasAlternateKey(s => new { s.TenantId, s.Id });
        b.HasIndex(s => new { s.TenantId, s.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne<AiScenarioVersion>().WithMany().HasForeignKey(s => new { s.TenantId, ScenarioId = s.Id, s.CurrentVersionId })
            .HasPrincipalKey(v => new { v.TenantId, v.ScenarioId, v.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AiScenarioDraftConfiguration : IEntityTypeConfiguration<AiScenarioDraft>
{
    public void Configure(EntityTypeBuilder<AiScenarioDraft> b)
    {
        b.ToTable("ai_scenario_draft"); b.ConfigureBaseEntity();
        b.Property(d => d.ConfigurationJson).IsRequired();
        b.HasIndex(d => new { d.TenantId, d.ScenarioId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne<AiScenario>().WithMany().HasForeignKey(d => new { d.TenantId, d.ScenarioId })
            .HasPrincipalKey(s => new { s.TenantId, s.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AiScenarioVersionConfiguration : IEntityTypeConfiguration<AiScenarioVersion>
{
    public void Configure(EntityTypeBuilder<AiScenarioVersion> b)
    {
        b.ToTable("ai_scenario_version"); b.ConfigureBaseEntity();
        b.Property(v => v.SnapshotJson).IsRequired();
        b.Property(v => v.ContentHash).HasMaxLength(64).IsRequired();
        b.Property(v => v.BuildIdentity).HasMaxLength(64).IsRequired();
        b.HasAlternateKey(v => new { v.TenantId, v.ScenarioId, v.Id });
        b.HasIndex(v => new { v.TenantId, v.ScenarioId, v.VersionNumber }).IsUnique();
        b.HasIndex(v => new { v.TenantId, v.ScenarioId, v.ContentHash });
        b.HasOne<AiScenario>().WithMany().HasForeignKey(v => new { v.TenantId, v.ScenarioId })
            .HasPrincipalKey(s => new { s.TenantId, s.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AiScenarioEvaluationConfiguration : IEntityTypeConfiguration<AiScenarioEvaluation>
{
    public void Configure(EntityTypeBuilder<AiScenarioEvaluation> b)
    {
        b.ToTable("ai_scenario_evaluation"); b.ConfigureBaseEntity();
        b.Property(e => e.ReportJson).IsRequired();
        b.Property(e => e.ReportHash).HasMaxLength(64).IsRequired();
        b.Property(e => e.Mode).HasMaxLength(16).IsRequired();
        b.Property(e => e.ModelFingerprint).HasMaxLength(64).IsRequired();
        b.HasAlternateKey(e => new { e.TenantId, e.ScenarioId, e.VersionId, e.Id });
        b.HasIndex(e => new { e.TenantId, e.VersionId, e.ReportHash }).IsUnique();
        b.HasOne<AiScenarioVersion>().WithMany().HasForeignKey(e => new { e.TenantId, e.ScenarioId, e.VersionId })
            .HasPrincipalKey(v => new { v.TenantId, v.ScenarioId, v.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AiScenarioReleaseEventConfiguration : IEntityTypeConfiguration<AiScenarioReleaseEvent>
{
    public void Configure(EntityTypeBuilder<AiScenarioReleaseEvent> b)
    {
        b.ToTable("ai_scenario_release_event"); b.ConfigureBaseEntity();
        b.Property(e => e.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(e => e.Reason).HasMaxLength(1000).IsRequired();
        b.HasIndex(e => new { e.TenantId, e.ScenarioId, e.Sequence }).IsUnique();
        b.HasIndex(e => new { e.TenantId, e.VersionId, e.CreatedAt });
        b.HasOne<AiScenarioVersion>().WithMany().HasForeignKey(e => new { e.TenantId, e.ScenarioId, e.VersionId })
            .HasPrincipalKey(v => new { v.TenantId, v.ScenarioId, v.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AiScenarioVersion>().WithMany().HasForeignKey(e => new { e.TenantId, e.ScenarioId, e.PreviousVersionId })
            .HasPrincipalKey(v => new { v.TenantId, v.ScenarioId, v.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AiScenarioEvaluation>().WithMany().HasForeignKey(e => new { e.TenantId, e.ScenarioId, e.VersionId, e.EvaluationId })
            .HasPrincipalKey(v => new { v.TenantId, v.ScenarioId, v.VersionId, v.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
