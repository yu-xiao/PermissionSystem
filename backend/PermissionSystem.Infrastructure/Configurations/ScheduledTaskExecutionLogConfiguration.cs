using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class ScheduledTaskExecutionLogConfiguration : IEntityTypeConfiguration<ScheduledTaskExecutionLog>
{
    public void Configure(EntityTypeBuilder<ScheduledTaskExecutionLog> builder)
    {
        builder.ToTable("ScheduledTaskExecutionLogs");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.JobId).HasMaxLength(128);
        builder.Property(entity => entity.JobType).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Message).HasMaxLength(1024);
        builder.Property(entity => entity.ParametersJson).HasMaxLength(4000);
        builder.Property(entity => entity.StartedAt).IsRequired();

        builder.HasIndex(entity => new { entity.TenantId, entity.ScheduledTaskId, entity.StartedAt });
    }
}
