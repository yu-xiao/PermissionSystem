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

        builder.Property(entity => entity.AgentCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.AgentVersion).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.PromptVersion).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.ModelName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.TraceId).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.EstimatedCost).HasPrecision(18, 6);
        builder.Property(entity => entity.ErrorCode).HasMaxLength(100);
        builder.Property(entity => entity.ErrorSummary).HasMaxLength(1000);

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
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.ActorUserId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.Status, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.TraceId });
        builder.HasIndex(entity => new { entity.TenantId, entity.ConversationId, entity.CreatedAt });
    }
}
