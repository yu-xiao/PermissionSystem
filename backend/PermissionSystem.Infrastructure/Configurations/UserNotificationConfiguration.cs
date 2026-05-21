using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("UserNotifications");
        builder.ConfigureBaseEntity();

        builder.HasOne(entity => entity.Notification)
            .WithMany(entity => entity.UserNotifications)
            .HasForeignKey(entity => entity.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entity => new { entity.TenantId, entity.UserId, entity.IsRead, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.UserId, entity.NotificationId }).IsUnique();
    }
}
