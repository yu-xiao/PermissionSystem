using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("NotificationTemplates");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.Code).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Type).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.TitleTemplate).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.ContentTemplate).HasMaxLength(4000).IsRequired();
        builder.Property(entity => entity.Status).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Remark).HasMaxLength(500);

        builder.HasIndex(entity => new { entity.TenantId, entity.Code }).IsUnique();
    }
}
