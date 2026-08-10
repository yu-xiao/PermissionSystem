using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.ToTable("wf_definition");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.Code).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.Property(entity => entity.Version).IsRequired().HasDefaultValue(1);
        builder.Property(entity => entity.Status).IsRequired().HasDefaultValue(WorkflowDefinitionStatus.Draft);
        builder.Property(entity => entity.IsPublished).IsRequired().HasDefaultValue(false);

        builder.HasIndex(entity => new { entity.TenantId, entity.Code, entity.Version })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.Status, entity.IsPublished });
    }
}
