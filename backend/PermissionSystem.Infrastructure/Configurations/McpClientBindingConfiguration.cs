using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class McpClientBindingConfiguration : IEntityTypeConfiguration<McpClientBinding>
{
    public void Configure(EntityTypeBuilder<McpClientBinding> builder)
    {
        builder.ToTable("mcp_client_binding");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.OAuthClientId).HasMaxLength(100).IsRequired();

        builder.HasOne<ApiClient>()
            .WithMany()
            .HasForeignKey(entity => entity.ApiClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => entity.OAuthClientId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.ApiClientId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.IsEnabled });
    }
}
