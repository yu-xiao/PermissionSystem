using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class WorkflowCcConfiguration : IEntityTypeConfiguration<WorkflowCc>
{
    public void Configure(EntityTypeBuilder<WorkflowCc> builder)
    {
        builder.ToTable("wf_cc");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.NodeKey).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.CcUserName).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.IsRead).IsRequired().HasDefaultValue(false);

        builder.HasOne(entity => entity.Instance)
            .WithMany(entity => entity.Ccs)
            .HasForeignKey(entity => entity.InstanceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.CcUserId, entity.IsRead, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.InstanceId, entity.CcUserId });
    }
}
