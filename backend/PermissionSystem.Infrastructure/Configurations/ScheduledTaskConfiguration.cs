using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class ScheduledTaskConfiguration : IEntityTypeConfiguration<ScheduledTask>
{
    public void Configure(EntityTypeBuilder<ScheduledTask> builder)
    {
        builder.ToTable("ScheduledTasks");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.Code).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.JobType).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.CronExpression).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Queue).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(512);
        builder.Property(entity => entity.ParametersJson).HasMaxLength(4000);
        builder.Property(entity => entity.LastRunMessage).HasMaxLength(1024);
        builder.Property(entity => entity.LastJobId).HasMaxLength(128);
        builder.Property(entity => entity.IsEnabled).HasDefaultValue(true);

        builder.HasIndex(entity => new { entity.TenantId, entity.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.IsEnabled });
        builder.HasMany(entity => entity.ExecutionLogs)
            .WithOne(entity => entity.ScheduledTask)
            .HasForeignKey(entity => entity.ScheduledTaskId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
