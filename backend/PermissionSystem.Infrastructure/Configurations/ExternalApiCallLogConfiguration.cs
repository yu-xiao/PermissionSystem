using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class ExternalApiCallLogConfiguration : IEntityTypeConfiguration<ExternalApiCallLog>
{
    public void Configure(EntityTypeBuilder<ExternalApiCallLog> builder)
    {
        builder.ToTable("ExternalApiCallLogs");

        builder.Property(entity => entity.Path).HasMaxLength(500).IsRequired();
        builder.Property(entity => entity.Method).HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.IpAddress).HasMaxLength(64);

        builder.HasIndex(entity => new { entity.TenantId, entity.ClientId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.Path, entity.CreatedAt });
    }
}
