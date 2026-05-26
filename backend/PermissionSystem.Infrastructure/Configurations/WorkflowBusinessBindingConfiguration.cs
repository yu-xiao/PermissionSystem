using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class WorkflowBusinessBindingConfiguration : IEntityTypeConfiguration<WorkflowBusinessBinding>
{
    public void Configure(EntityTypeBuilder<WorkflowBusinessBinding> builder)
    {
        builder.ToTable("wf_business_binding");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.BusinessType).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.BusinessName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.DefinitionCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.DefinitionName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.IsEnabled).IsRequired().HasDefaultValue(true);
        builder.Property(entity => entity.Remark).HasMaxLength(1000);

        builder.HasOne(entity => entity.Definition)
            .WithMany(entity => entity.BusinessBindings)
            .HasForeignKey(entity => entity.DefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.BusinessType, entity.IsDeleted }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.DefinitionId, entity.IsEnabled });
    }
}
