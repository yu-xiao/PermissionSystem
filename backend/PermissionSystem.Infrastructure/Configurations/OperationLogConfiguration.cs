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

        builder.Property(entity => entity.UserName).HasMaxLength(128);
        builder.Property(entity => entity.Module).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Action).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Method).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.RequestPath).HasMaxLength(512);
        builder.Property(entity => entity.RequestMethod).HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.RequestBody).HasMaxLength(4000);
        builder.Property(entity => entity.ResponseBody).HasMaxLength(4000);
        builder.Property(entity => entity.IpAddress).HasMaxLength(64);
        builder.Property(entity => entity.UserAgent).HasMaxLength(512);
        builder.Property(entity => entity.TraceId).HasMaxLength(128);

        builder.HasIndex(entity => new { entity.TenantId, entity.CreatedAt });
        builder.HasIndex(entity => entity.UserId);
        builder.HasIndex(entity => entity.TraceId);
    }
}
