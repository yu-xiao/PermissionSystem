using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class DictionaryTypeConfiguration : IEntityTypeConfiguration<DictionaryType>
{
    public void Configure(EntityTypeBuilder<DictionaryType> builder)
    {
        builder.ToTable("DictionaryTypes");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.Code).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(512);
        builder.Property(entity => entity.Status).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Sort).IsRequired();

        builder.HasIndex(entity => new { entity.TenantId, entity.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.Status });
    }
}
