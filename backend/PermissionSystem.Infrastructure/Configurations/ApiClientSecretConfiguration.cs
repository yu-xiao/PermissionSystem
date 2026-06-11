using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class ApiClientSecretConfiguration : IEntityTypeConfiguration<ApiClientSecret>
{
    public void Configure(EntityTypeBuilder<ApiClientSecret> builder)
    {
        builder.ToTable("ApiClientSecrets");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.SecretHash).HasMaxLength(128).IsRequired();

        builder.HasIndex(entity => new { entity.TenantId, entity.ClientId });
        builder.HasIndex(entity => new { entity.TenantId, entity.SecretHash }).IsUnique();
    }
}
