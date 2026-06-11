using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("WebhookSubscriptions");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.EventType).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.TargetUrl).HasMaxLength(1000).IsRequired();
        builder.Property(entity => entity.Secret).HasMaxLength(2000).IsRequired();
        builder.Property(entity => entity.RetryCount).HasDefaultValue(3);

        builder.HasIndex(entity => new { entity.TenantId, entity.EventType, entity.IsEnabled });
    }
}
