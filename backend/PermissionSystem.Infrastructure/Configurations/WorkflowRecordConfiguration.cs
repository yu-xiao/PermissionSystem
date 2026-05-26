using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class WorkflowRecordConfiguration : IEntityTypeConfiguration<WorkflowRecord>
{
    public void Configure(EntityTypeBuilder<WorkflowRecord> builder)
    {
        builder.ToTable("wf_record");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.NodeKey).HasMaxLength(100);
        builder.Property(entity => entity.NodeName).HasMaxLength(200);
        builder.Property(entity => entity.OperatorUserName).HasMaxLength(100);
        builder.Property(entity => entity.Action).IsRequired();
        builder.Property(entity => entity.Comment).HasMaxLength(1000);
        builder.Property(entity => entity.OperatedAt).IsRequired();

        builder.HasOne(entity => entity.Instance)
            .WithMany(entity => entity.Records)
            .HasForeignKey(entity => entity.InstanceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.Task)
            .WithMany(entity => entity.Records)
            .HasForeignKey(entity => entity.TaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.InstanceId, entity.OperatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.OperatorUserId, entity.OperatedAt });
    }
}
