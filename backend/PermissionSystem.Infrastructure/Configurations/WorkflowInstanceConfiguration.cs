using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
{
    public void Configure(EntityTypeBuilder<WorkflowInstance> builder)
    {
        builder.ToTable("wf_instance");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.DefinitionCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.DefinitionName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.BusinessType).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.BusinessId).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.BusinessTitle).HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.StarterUserName).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Status).IsRequired().HasDefaultValue(WorkflowInstanceStatus.Running);
        builder.Property(entity => entity.CurrentNodeKey).HasMaxLength(100);
        builder.Property(entity => entity.FormDataJson);
        builder.Property(entity => entity.StartedAt).IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();

        builder.HasOne(entity => entity.Definition)
            .WithMany(entity => entity.Instances)
            .HasForeignKey(entity => entity.DefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.BusinessType, entity.BusinessId })
            .IsUnique()
            .HasFilter("[Status] = 0 AND [IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.StarterUserId, entity.Status, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.Status, entity.CreatedAt });
    }
}
