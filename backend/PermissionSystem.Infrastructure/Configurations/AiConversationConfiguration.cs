using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class AiConversationConfiguration : IEntityTypeConfiguration<AiConversation>
{
    public void Configure(EntityTypeBuilder<AiConversation> builder)
    {
        builder.ToTable("ai_conversation");
        builder.ConfigureBaseEntity();

        builder.ToTable("ai_conversation", t => t.HasCheckConstraint("CK_ai_conversation_ScenarioVersion", "([ScenarioId] IS NULL AND [ScenarioVersionId] IS NULL) OR ([ScenarioId] IS NOT NULL AND [ScenarioVersionId] IS NOT NULL)"));
        builder.HasOne<AiScenarioVersion>().WithMany()
            .HasForeignKey(e => new { e.TenantId, e.ScenarioId, e.ScenarioVersionId })
            .HasPrincipalKey(v => new { v.TenantId, v.ScenarioId, v.Id }).OnDelete(DeleteBehavior.Restrict);


        builder.Property(entity => entity.AgentCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.AgentVersion).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.LastMessageAt).IsRequired();
        builder.Property(entity => entity.RetentionUntil).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.UserId, entity.LastMessageAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.Status, entity.LastMessageAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.RetentionUntil });
    }
}
