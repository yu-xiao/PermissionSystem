using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class JobExecutionLogConfiguration : IEntityTypeConfiguration<JobExecutionLog>
{
    public void Configure(EntityTypeBuilder<JobExecutionLog> builder)
    {
        builder.ToTable("JobExecutionLogs");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.JobName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.JobId).HasMaxLength(128);
        builder.Property(entity => entity.Status).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.ErrorMessage).HasMaxLength(2000);
        builder.Property(entity => entity.TraceId).HasMaxLength(128);
        builder.Property(entity => entity.StartedAt).IsRequired();

        builder.HasIndex(entity => new { entity.TenantId, entity.JobName, entity.StartedAt });
        builder.HasIndex(entity => entity.JobId);
        builder.HasIndex(entity => entity.TraceId);
    }
}
