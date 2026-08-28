using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class AiDocumentDraftValidationConfiguration : IEntityTypeConfiguration<AiDocumentDraftValidation>
{
    public void Configure(EntityTypeBuilder<AiDocumentDraftValidation> builder)
    {
        builder.ToTable("ai_document_draft_validation");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.PayloadHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(entity => entity.ErrorsJson).HasColumnType("nvarchar(max)").IsRequired();

        builder.HasOne<AiDocumentDraft>()
            .WithMany()
            .HasForeignKey(entity => entity.DraftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.DraftId, entity.DraftVersion })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.IsValid, entity.ValidatedAt });
    }
}
