using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class SsoRoleMappingConfiguration : IEntityTypeConfiguration<SsoRoleMapping>
{
    public void Configure(EntityTypeBuilder<SsoRoleMapping> builder)
    {
        builder.ToTable("sso_role_mapping");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.ExternalRole).HasMaxLength(256).IsRequired();

        builder.HasOne(entity => entity.Provider)
            .WithMany(entity => entity.RoleMappings)
            .HasForeignKey(entity => entity.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.LocalRole)
            .WithMany()
            .HasForeignKey(entity => entity.LocalRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.ProviderId, entity.ExternalRole, entity.LocalRoleId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.ProviderId });
        builder.HasIndex(entity => entity.LocalRoleId);
    }
}
