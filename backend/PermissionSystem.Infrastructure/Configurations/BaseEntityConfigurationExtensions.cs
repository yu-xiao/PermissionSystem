using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Common;

namespace PermissionSystem.Infrastructure.Configurations;

internal static class BaseEntityConfigurationExtensions
{
    public static void ConfigureBaseEntity<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : BaseEntity
    {
        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.TenantId).IsRequired();
        builder.Property(entity => entity.CreatedAt).IsRequired();
        builder.Property(entity => entity.CreatedBy);
        builder.Property(entity => entity.UpdatedAt);
        builder.Property(entity => entity.UpdatedBy);
        builder.Property(entity => entity.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasIndex(entity => entity.TenantId);
        builder.HasIndex(entity => entity.IsDeleted);
    }
}
