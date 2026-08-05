using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.Code).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(512);
        builder.Property(entity => entity.Status).IsRequired();
        builder.Property(entity => entity.InitializationStep).HasMaxLength(64);
        builder.Property(entity => entity.InitializationProgress).IsRequired().HasDefaultValue(0);
        builder.Property(entity => entity.InitializationAttempts).IsRequired().HasDefaultValue(0);
        builder.Property(entity => entity.InitializationJobId).HasMaxLength(128);
        builder.Property(entity => entity.InitializationError).HasMaxLength(2000);
        builder.Property(entity => entity.StatusChangedAt).IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();

        builder.HasIndex(entity => entity.Code).IsUnique();
        builder.HasIndex(entity => entity.Status);
    }
}
