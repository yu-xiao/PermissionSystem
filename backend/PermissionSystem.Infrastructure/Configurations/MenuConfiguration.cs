using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> builder)
    {
        builder.ToTable("Menus");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.Name).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Path).HasMaxLength(256);
        builder.Property(entity => entity.Component).HasMaxLength(256);
        builder.Property(entity => entity.Redirect).HasMaxLength(256);
        builder.Property(entity => entity.Icon).HasMaxLength(128);
        builder.Property(entity => entity.Sort).IsRequired();
        builder.Property(entity => entity.Visible).IsRequired().HasDefaultValue(true);
        builder.Property(entity => entity.KeepAlive).IsRequired().HasDefaultValue(false);
        builder.Property(entity => entity.MenuType).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.PermissionCode).HasMaxLength(128);

        builder.HasOne(entity => entity.Parent)
            .WithMany(entity => entity.Children)
            .HasForeignKey(entity => entity.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.PermissionCode });
    }
}
