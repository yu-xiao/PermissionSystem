using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class FileResourceConfiguration : IEntityTypeConfiguration<FileResource>
{
    public void Configure(EntityTypeBuilder<FileResource> builder)
    {
        builder.ToTable("FileResources");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.OriginalName).HasMaxLength(255).IsRequired();
        builder.Property(entity => entity.FileName).HasMaxLength(255).IsRequired();
        builder.Property(entity => entity.Extension).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Size).IsRequired();
        builder.Property(entity => entity.StorageProvider).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.BucketName).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.ObjectKey).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.Url).HasMaxLength(1024);
        builder.Property(entity => entity.Md5).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.BusinessType).HasMaxLength(128);
        builder.Property(entity => entity.BusinessId);

        builder.HasIndex(entity => new { entity.TenantId, entity.Md5 });
        builder.HasIndex(entity => new { entity.TenantId, entity.BusinessType, entity.BusinessId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.CreatedAt });
    }
}
