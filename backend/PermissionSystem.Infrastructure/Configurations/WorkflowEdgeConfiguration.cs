using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class WorkflowEdgeConfiguration : IEntityTypeConfiguration<WorkflowEdge>
{
    public void Configure(EntityTypeBuilder<WorkflowEdge> builder)
    {
        builder.ToTable("wf_edge");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.FromNodeKey).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ToNodeKey).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.IsDefault).IsRequired().HasDefaultValue(false);
        builder.Property(entity => entity.Sort).IsRequired();

        builder.HasOne(entity => entity.Definition)
            .WithMany(entity => entity.Edges)
            .HasForeignKey(entity => entity.DefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.Condition)
            .WithMany(entity => entity.Edges)
            .HasForeignKey(entity => entity.ConditionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.DefinitionId, entity.FromNodeKey });
        builder.HasIndex(entity => new { entity.TenantId, entity.DefinitionId, entity.ToNodeKey });
        builder.HasIndex(entity => new { entity.TenantId, entity.DefinitionId, entity.Sort });
    }
}
