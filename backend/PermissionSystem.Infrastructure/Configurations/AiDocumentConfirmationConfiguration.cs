using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class AiDocumentConfirmationConfiguration : IEntityTypeConfiguration<AiDocumentConfirmation>
{
    public void Configure(EntityTypeBuilder<AiDocumentConfirmation> builder)
    {
        builder.ToTable("ai_document_confirmation");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.PayloadHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(entity => entity.HandlerVersion).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasOne<AiDocumentDraft>()
            .WithMany()
            .HasForeignKey(entity => entity.DraftId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AiRun>()
            .WithMany()
            .HasForeignKey(entity => entity.RunId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.DraftId, entity.DraftVersion })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.ActorUserId, entity.Status, entity.ExpiresAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.RunId, entity.CreatedAt });
    }
}
