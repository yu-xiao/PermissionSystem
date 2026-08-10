using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class RoleDataScopeConfiguration : IEntityTypeConfiguration<RoleDataScope>
{
    public void Configure(EntityTypeBuilder<RoleDataScope> builder)
    {
        builder.ToTable("RoleDataScopes");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.ScopeType).IsRequired();
        builder.Property(entity => entity.CustomDepartmentIds).HasMaxLength(2000);

        builder.HasOne(entity => entity.Role)
            .WithOne(entity => entity.DataScope)
            .HasForeignKey<RoleDataScope>(entity => entity.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.RoleId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
