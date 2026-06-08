using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class ReportDefinitionConfiguration : IEntityTypeConfiguration<ReportDefinition>
{
    public void Configure(EntityTypeBuilder<ReportDefinition> builder)
    {
        builder.ToTable("ReportDefinitions");

        builder.Property(entity => entity.ReportCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ReportName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Category).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.DataSourceType).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.SqlText).HasColumnType("nvarchar(max)");
        builder.Property(entity => entity.ApiUrl).HasMaxLength(500);
        builder.Property(entity => entity.ColumnsJson).HasColumnType("nvarchar(max)");
        builder.Property(entity => entity.ParamsJson).HasColumnType("nvarchar(max)");
        builder.Property(entity => entity.Remark).HasMaxLength(500);

        builder.HasIndex(entity => new { entity.TenantId, entity.ReportCode }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.Category, entity.IsEnabled });
    }
}
