using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class SsoLoginLogConfiguration : IEntityTypeConfiguration<SsoLoginLog>
{
    public void Configure(EntityTypeBuilder<SsoLoginLog> builder)
    {
        builder.ToTable("sso_login_log");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.ProviderCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ProviderName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.ProviderType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(entity => entity.ExternalUserId).HasMaxLength(256);
        builder.Property(entity => entity.ExternalUserName).HasMaxLength(256);
        builder.Property(entity => entity.LocalUserName).HasMaxLength(128);
        builder.Property(entity => entity.LoginResult)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(entity => entity.FailureReason).HasMaxLength(512);
        builder.Property(entity => entity.IpAddress).HasMaxLength(64);
        builder.Property(entity => entity.UserAgent).HasMaxLength(512);
        builder.Property(entity => entity.TraceId).HasMaxLength(128);

        builder.HasIndex(entity => new { entity.TenantId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.ProviderCode, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.LoginResult, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.LocalUserId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.ProviderCode, entity.ExternalUserId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.TraceId });
    }
}
