using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class WebhookDeliveryLogConfiguration : IEntityTypeConfiguration<WebhookDeliveryLog>
{
    public void Configure(EntityTypeBuilder<WebhookDeliveryLog> builder)
    {
        builder.ToTable("WebhookDeliveryLogs");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.EventType).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Payload).IsRequired();
        builder.Property(entity => entity.Status).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.ResponseBody).HasMaxLength(4000);

        builder.HasIndex(entity => new { entity.TenantId, entity.SubscriptionId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.EventType, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.Status, entity.CreatedAt });
    }
}
