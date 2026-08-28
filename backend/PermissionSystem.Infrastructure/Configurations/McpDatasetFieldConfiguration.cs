using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class McpDatasetFieldConfiguration : IEntityTypeConfiguration<McpDatasetField>
{
    public void Configure(EntityTypeBuilder<McpDatasetField> builder)
    {
        builder.ToTable("mcp_dataset_field");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.FieldCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.DataType).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.DataClassification).HasMaxLength(32).IsRequired();

        builder.HasOne<McpDatasetDefinition>()
            .WithMany()
            .HasForeignKey(entity => entity.DatasetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.DatasetId, entity.FieldCode })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.DatasetId, entity.IsDefault });
    }
}
