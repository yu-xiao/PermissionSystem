using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class SsoProviderConfiguration : IEntityTypeConfiguration<SsoProvider>
{
    public void Configure(EntityTypeBuilder<SsoProvider> builder)
    {
        builder.ToTable("sso_provider");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.ProviderCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ProviderName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.ProviderType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(entity => entity.Enabled).IsRequired().HasDefaultValue(true);
        builder.Property(entity => entity.Authority).HasMaxLength(1000);
        builder.Property(entity => entity.MetadataAddress).HasMaxLength(1000);
        builder.Property(entity => entity.ClientId).HasMaxLength(256);
        builder.Property(entity => entity.ClientSecretEncrypted).HasMaxLength(2000);
        builder.Property(entity => entity.Scopes).HasMaxLength(1000).IsRequired();
        builder.Property(entity => entity.CallbackPath).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.ResponseType).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.UsePkce).IsRequired().HasDefaultValue(true);
        builder.Property(entity => entity.GetClaimsFromUserInfoEndpoint).IsRequired().HasDefaultValue(true);
        builder.Property(entity => entity.UserIdClaim).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.UserNameClaim).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.EmailClaim).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.PhoneClaim).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.DisplayNameClaim).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.RoleClaim).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.DepartmentClaim).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.AutoCreateUser).IsRequired().HasDefaultValue(false);
        builder.Property(entity => entity.AutoBindUser).IsRequired().HasDefaultValue(true);
        builder.Property(entity => entity.DefaultRoleIds).HasMaxLength(2000);
        builder.Property(entity => entity.AllowLocalLoginFallback).IsRequired().HasDefaultValue(true);
        builder.Property(entity => entity.LogoutRedirectUri).HasMaxLength(1000);
        builder.Property(entity => entity.Remark).HasMaxLength(500);

        builder.HasIndex(entity => new { entity.TenantId, entity.ProviderCode }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.ProviderType, entity.Enabled });
    }
}
