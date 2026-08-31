using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class McpDatasetDefinitionConfiguration : IEntityTypeConfiguration<McpDatasetDefinition>
{
    public void Configure(EntityTypeBuilder<McpDatasetDefinition> builder)
    {
        builder.ToTable("mcp_dataset_definition");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.DatasetCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.DatasetName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Version).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.Property(entity => entity.DataClassification).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.HandlerCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.SchemaHash).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.PublicationStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(entity => new { entity.TenantId, entity.DatasetCode, entity.Version })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.IsEnabled, entity.DatasetCode });
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.PublicationStatus,
            entity.IsEnabled,
            entity.DatasetCode
        });
    }
}
