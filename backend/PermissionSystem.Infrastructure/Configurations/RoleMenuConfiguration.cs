using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class RoleMenuConfiguration : IEntityTypeConfiguration<RoleMenu>
{
    public void Configure(EntityTypeBuilder<RoleMenu> builder)
    {
        builder.ToTable("RoleMenus");
        builder.ConfigureBaseEntity();

        builder.HasOne(entity => entity.Role)
            .WithMany(entity => entity.RoleMenus)
            .HasForeignKey(entity => entity.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.Menu)
            .WithMany(entity => entity.RoleMenus)
            .HasForeignKey(entity => entity.MenuId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.RoleId, entity.MenuId }).IsUnique();
    }
}
