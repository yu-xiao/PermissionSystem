using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class WorkflowConditionConfiguration : IEntityTypeConfiguration<WorkflowCondition>
{
    public void Configure(EntityTypeBuilder<WorkflowCondition> builder)
    {
        builder.ToTable("wf_condition");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.NodeKey).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ConditionName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.ExpressionJson).IsRequired();
        builder.Property(entity => entity.Sort).IsRequired();

        builder.HasOne(entity => entity.Definition)
            .WithMany(entity => entity.Conditions)
            .HasForeignKey(entity => entity.DefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.DefinitionId, entity.NodeKey, entity.Sort });
    }
}
