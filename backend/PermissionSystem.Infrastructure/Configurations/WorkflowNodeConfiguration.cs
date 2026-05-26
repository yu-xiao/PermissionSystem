using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class WorkflowNodeConfiguration : IEntityTypeConfiguration<WorkflowNode>
{
    public void Configure(EntityTypeBuilder<WorkflowNode> builder)
    {
        builder.ToTable("wf_node");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.NodeKey).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.NodeName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.NodeType).IsRequired();
        builder.Property(entity => entity.ApproverIds).HasMaxLength(2000);
        builder.Property(entity => entity.ConfigJson);
        builder.Property(entity => entity.PositionX).HasPrecision(18, 2);
        builder.Property(entity => entity.PositionY).HasPrecision(18, 2);
        builder.Property(entity => entity.Sort).IsRequired();

        builder.HasOne(entity => entity.Definition)
            .WithMany(entity => entity.Nodes)
            .HasForeignKey(entity => entity.DefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.DefinitionId, entity.NodeKey }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.DefinitionId, entity.NodeType });
    }
}
