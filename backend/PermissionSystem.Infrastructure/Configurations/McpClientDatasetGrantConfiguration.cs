using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class McpClientDatasetGrantConfiguration : IEntityTypeConfiguration<McpClientDatasetGrant>
{
    public void Configure(EntityTypeBuilder<McpClientDatasetGrant> builder)
    {
        builder.ToTable("mcp_client_dataset_grant");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.AllowedFieldsJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(entity => entity.ApprovedSchemaHash).HasMaxLength(64).IsRequired();

        builder.HasOne<McpClientBinding>()
            .WithMany()
            .HasForeignKey(entity => entity.ClientBindingId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<McpDatasetDefinition>()
            .WithMany()
            .HasForeignKey(entity => entity.DatasetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.ClientBindingId, entity.DatasetId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.ClientBindingId, entity.IsEnabled });
    }
}
