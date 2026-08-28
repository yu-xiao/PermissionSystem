using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class AiDocumentDraftConfiguration : IEntityTypeConfiguration<AiDocumentDraft>
{
    public void Configure(EntityTypeBuilder<AiDocumentDraft> builder)
    {
        builder.ToTable("ai_document_draft");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.SourceInvocationId).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.BusinessType).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.HandlerVersion).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(entity => entity.PayloadHash).HasMaxLength(64).IsFixedLength().IsRequired();

        builder.HasOne<AiConversation>()
            .WithMany()
            .HasForeignKey(entity => entity.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AiRun>()
            .WithMany()
            .HasForeignKey(entity => entity.RunId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.SourceInvocationId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.ActorUserId, entity.Status, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.ConversationId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.RunId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.Status, entity.ExpiresAt });
    }
}
