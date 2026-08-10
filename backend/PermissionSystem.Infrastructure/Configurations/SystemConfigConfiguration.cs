using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class SystemConfigConfiguration : IEntityTypeConfiguration<SystemConfig>
{
    public void Configure(EntityTypeBuilder<SystemConfig> builder)
    {
        builder.ToTable("SystemConfigs");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.ConfigKey).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.ConfigValue).HasMaxLength(4000).IsRequired();
        builder.Property(entity => entity.ConfigType).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.GroupCode).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(512);
        builder.Property(entity => entity.IsEncrypted).IsRequired().HasDefaultValue(false);
        builder.Property(entity => entity.IsSystem).IsRequired().HasDefaultValue(false);
        builder.Property(entity => entity.Status).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Sort).IsRequired();

        builder.HasIndex(entity => new { entity.TenantId, entity.ConfigKey })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.GroupCode, entity.Status });
    }
}
