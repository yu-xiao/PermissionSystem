using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class DeadLetterMessageConfiguration : IEntityTypeConfiguration<DeadLetterMessage>
{
    public void Configure(EntityTypeBuilder<DeadLetterMessage> builder)
    {
        builder.ToTable("DeadLetterMessages");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.MessageId).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Consumer).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.SourceQueue).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Exchange).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.RoutingKey).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.MessageType).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.Payload).IsRequired();
        builder.Property(entity => entity.Headers);
        builder.Property(entity => entity.FailureReason).HasMaxLength(2000).IsRequired();
        builder.Property(entity => entity.Status).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.DispositionRemark).HasMaxLength(500);

        builder.HasIndex(entity => new { entity.TenantId, entity.MessageId, entity.Consumer }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.Status, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.SourceQueue, entity.CreatedAt });
    }
}
