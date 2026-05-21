using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.Type).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Content).HasMaxLength(4000).IsRequired();
        builder.Property(entity => entity.SenderName).HasMaxLength(100);
        builder.Property(entity => entity.LinkUrl).HasMaxLength(500);
        builder.Property(entity => entity.Payload).HasMaxLength(4000);

        builder.HasIndex(entity => new { entity.TenantId, entity.Type, entity.CreatedAt });
    }
}
