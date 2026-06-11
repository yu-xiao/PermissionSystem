using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class WorkflowTaskConfiguration : IEntityTypeConfiguration<WorkflowTask>
{
    public void Configure(EntityTypeBuilder<WorkflowTask> builder)
    {
        builder.ToTable("wf_task");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.NodeKey).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.NodeName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.ApproverUserName).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Status).IsRequired().HasDefaultValue(WorkflowTaskStatus.Pending);
        builder.Property(entity => entity.AssignedAt).IsRequired();

        builder.HasOne(entity => entity.Instance)
            .WithMany(entity => entity.Tasks)
            .HasForeignKey(entity => entity.InstanceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.ApproverUserId, entity.Status, entity.AssignedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.InstanceId, entity.NodeKey });
        builder.HasIndex(entity => new { entity.TenantId, entity.InstanceId, entity.ApproverUserId });
    }
}
