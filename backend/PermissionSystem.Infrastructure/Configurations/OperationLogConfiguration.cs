using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class OperationLogConfiguration : IEntityTypeConfiguration<OperationLog>
{
    public void Configure(EntityTypeBuilder<OperationLog> builder)
    {
        builder.ToTable("OperationLogs");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.Module).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Action).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.RequestPath).HasMaxLength(512);
        builder.Property(entity => entity.HttpMethod).HasMaxLength(16);
        builder.Property(entity => entity.IpAddress).HasMaxLength(64);
        builder.Property(entity => entity.UserAgent).HasMaxLength(512);
        builder.Property(entity => entity.Message).HasMaxLength(1024);
        builder.Property(entity => entity.OperatedAt).IsRequired();

        builder.HasIndex(entity => new { entity.TenantId, entity.OperatedAt });
        builder.HasIndex(entity => entity.OperatorUserId);
    }
}
