using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class AiRunConfiguration : IEntityTypeConfiguration<AiRun>
{
    public void Configure(EntityTypeBuilder<AiRun> builder)
    {
        builder.ToTable("ai_run");
        builder.ConfigureBaseEntity();
        builder.Property(e => e.ExecutionMode).HasMaxLength(32);
        builder.Property(e => e.ActorSessionId).HasMaxLength(128);
        builder.Property(e => e.SubmissionHash).HasMaxLength(64);
        builder.Property(e => e.RequestHash).HasMaxLength(64);
        builder.HasIndex(e => new { e.TenantId, e.ActorUserId, e.ConversationId, e.SubmissionHash })
            .IsUnique().HasFilter("[SubmissionHash] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.ExecutionMode, e.Status, e.CreatedAt });
        builder.HasIndex(e => new { e.TenantId, e.ConversationId })
            .IsUnique().HasFilter("[IsDeleted] = 0 AND [Status] IN ('Pending', 'Running')");

        builder.ToTable("ai_run", t => t.HasCheckConstraint("CK_ai_run_ScenarioVersion", "([ScenarioId] IS NULL AND [ScenarioVersionId] IS NULL) OR ([ScenarioId] IS NOT NULL AND [ScenarioVersionId] IS NOT NULL)"));
        builder.HasOne<AiScenarioVersion>().WithMany()
            .HasForeignKey(e => new { e.TenantId, e.ScenarioId, e.ScenarioVersionId })
            .HasPrincipalKey(v => new { v.TenantId, v.ScenarioId, v.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.ScenarioContentHash).HasMaxLength(64);
        builder.Property(e => e.BuildIdentity).HasMaxLength(64);
        builder.Property(e => e.ExecutionConfigurationHash).HasMaxLength(64);


        builder.Property(entity => entity.AgentCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.AgentVersion).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.PromptVersion).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.ModelName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.TraceId).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.EstimatedCost).HasPrecision(18, 6);
        builder.Property(entity => entity.ErrorCode).HasMaxLength(100);
        builder.Property(entity => entity.ErrorSummary).HasMaxLength(1000);
        builder.Property(entity => entity.ExecutionLeaseId).IsRequired();

        builder.HasOne<AiConversation>()
            .WithMany()
            .HasForeignKey(entity => entity.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AiMessage>()
            .WithMany()
            .HasForeignKey(entity => entity.RequestMessageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AiMessage>()
            .WithMany()
            .HasForeignKey(entity => entity.ResponseMessageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AiProviderConfig>()
            .WithMany()
            .HasForeignKey(entity => entity.ProviderConfigId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AiProviderConfig>()
            .WithMany()
            .HasForeignKey(entity => entity.FinalProviderConfigId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AiRun>()
            .WithMany()
            .HasForeignKey(entity => entity.RetryOfRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.ActorUserId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.Status, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.TraceId });
        builder.HasIndex(entity => new { entity.TenantId, entity.ConversationId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.FinalProviderConfigId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.Status, entity.LastHeartbeatAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.RetryOfRunId });
    }
}
