using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class DictionaryItemConfiguration : IEntityTypeConfiguration<DictionaryItem>
{
    public void Configure(EntityTypeBuilder<DictionaryItem> builder)
    {
        builder.ToTable("DictionaryItems");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.TypeCode).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Label).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Value).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Color).HasMaxLength(32);
        builder.Property(entity => entity.CssClass).HasMaxLength(128);
        builder.Property(entity => entity.IsDefault).IsRequired().HasDefaultValue(false);
        builder.Property(entity => entity.Status).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Sort).IsRequired();
        builder.Property(entity => entity.Remark).HasMaxLength(512);

        builder.HasIndex(entity => new { entity.TenantId, entity.TypeCode, entity.Value })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.TypeCode, entity.Status });
    }
}
