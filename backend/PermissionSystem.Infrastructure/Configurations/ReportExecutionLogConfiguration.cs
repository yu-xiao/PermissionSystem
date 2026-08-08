using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class ReportExecutionLogConfiguration : IEntityTypeConfiguration<ReportExecutionLog>
{
    public void Configure(EntityTypeBuilder<ReportExecutionLog> builder)
    {
        builder.ToTable("ReportExecutionLogs");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.ReportCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ExecuteUserName).HasMaxLength(100);
        builder.Property(entity => entity.ParamsJson).HasColumnType("nvarchar(max)");
        builder.Property(entity => entity.IsSuccess).IsRequired();
        builder.Property(entity => entity.FailureReason).HasMaxLength(500);

        builder.HasIndex(entity => new { entity.TenantId, entity.ReportId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.ReportCode, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.CreatedAt });
    }
}
