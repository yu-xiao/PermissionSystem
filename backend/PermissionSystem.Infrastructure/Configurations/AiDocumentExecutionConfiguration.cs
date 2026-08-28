using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class AiDocumentExecutionConfiguration : IEntityTypeConfiguration<AiDocumentExecution>
{
    public void Configure(EntityTypeBuilder<AiDocumentExecution> builder)
    {
        builder.ToTable("ai_document_execution");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.BusinessType).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.BusinessIdempotencyKey).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.BusinessNo).HasMaxLength(100);
        builder.Property(entity => entity.BusinessStatus).HasMaxLength(32);
        builder.Property(entity => entity.TraceId).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.OutboxMessageId).HasMaxLength(128);
        builder.Property(entity => entity.ErrorCode).HasMaxLength(100);
        builder.Property(entity => entity.ErrorSummary).HasMaxLength(1000);

        builder.HasOne<AiDocumentConfirmation>()
            .WithMany()
            .HasForeignKey(entity => entity.ConfirmationId)
            .OnDelete(DeleteBehavior.Restrict);
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

        builder.HasIndex(entity => new { entity.TenantId, entity.ConfirmationId, entity.ConfirmationVersion })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.BusinessIdempotencyKey })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.BusinessEntityId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.RunId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.TraceId });
        builder.HasIndex(entity => new { entity.TenantId, entity.Status, entity.CreatedAt });
    }
}
