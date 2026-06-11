using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class LoginFailureRecordConfiguration : IEntityTypeConfiguration<LoginFailureRecord>
{
    public void Configure(EntityTypeBuilder<LoginFailureRecord> builder)
    {
        builder.ToTable("LoginFailureRecords");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.UserName).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.IpAddress).HasMaxLength(64);

        builder.HasIndex(entity => new { entity.TenantId, entity.UserName, entity.IpAddress }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.LockedUntil });
    }
}
