using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class SsoUserBindingConfiguration : IEntityTypeConfiguration<SsoUserBinding>
{
    public void Configure(EntityTypeBuilder<SsoUserBinding> builder)
    {
        builder.ToTable("sso_user_binding");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.ProviderCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ExternalUserId).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.ExternalUserName).HasMaxLength(256);
        builder.Property(entity => entity.ExternalEmail).HasMaxLength(256);
        builder.Property(entity => entity.ExternalPhone).HasMaxLength(64);
        builder.Property(entity => entity.ClaimsJson).HasColumnType("nvarchar(max)");

        builder.HasOne(entity => entity.Provider)
            .WithMany(entity => entity.UserBindings)
            .HasForeignKey(entity => entity.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.LocalUser)
            .WithMany()
            .HasForeignKey(entity => entity.LocalUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.ProviderId, entity.ExternalUserId }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.ProviderId, entity.LocalUserId }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.ProviderCode });
        builder.HasIndex(entity => new { entity.TenantId, entity.ExternalEmail });
        builder.HasIndex(entity => new { entity.TenantId, entity.ExternalPhone });
        builder.HasIndex(entity => entity.LocalUserId);
    }
}
