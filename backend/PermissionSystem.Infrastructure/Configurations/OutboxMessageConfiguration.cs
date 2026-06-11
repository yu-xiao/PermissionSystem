using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.MessageId).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Exchange).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.RoutingKey).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.MessageType).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.Payload).IsRequired();
        builder.Property(entity => entity.Headers);
        builder.Property(entity => entity.Status).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.RetryCount).IsRequired();
        builder.Property(entity => entity.ErrorMessage).HasMaxLength(2000);

        builder.HasIndex(entity => new { entity.TenantId, entity.MessageId }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.Status, entity.NextRetryAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.Status, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.Status, entity.NextRetryAt, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.MessageType, entity.CreatedAt });
    }
}
