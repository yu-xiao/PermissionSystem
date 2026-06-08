using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class ApiClientConfiguration : IEntityTypeConfiguration<ApiClient>
{
    public void Configure(EntityTypeBuilder<ApiClient> builder)
    {
        builder.ToTable("ApiClients");

        builder.Property(entity => entity.ClientCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ClientName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(500);
        builder.Property(entity => entity.AllowedScopes).HasMaxLength(1000);
        builder.Property(entity => entity.AllowedIpList).HasMaxLength(1000);
        builder.Property(entity => entity.RateLimitPerMinute).HasDefaultValue(60);

        builder.HasIndex(entity => new { entity.TenantId, entity.ClientCode }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.IsEnabled });
    }
}
