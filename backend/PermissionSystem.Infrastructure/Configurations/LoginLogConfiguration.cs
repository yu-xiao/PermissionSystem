using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class LoginLogConfiguration : IEntityTypeConfiguration<LoginLog>
{
    public void Configure(EntityTypeBuilder<LoginLog> builder)
    {
        builder.ToTable("LoginLogs");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.UserName).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.LoginType).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.IpAddress).HasMaxLength(64);
        builder.Property(entity => entity.UserAgent).HasMaxLength(512);
        builder.Property(entity => entity.LoginResult).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.FailureReason).HasMaxLength(512);
        builder.Property(entity => entity.TraceId).HasMaxLength(128);

        builder.HasIndex(entity => new { entity.TenantId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.UserId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.TraceId });
    }
}
