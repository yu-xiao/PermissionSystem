using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class StateTransitionConfiguration : IEntityTypeConfiguration<StateTransition>
{
    public void Configure(EntityTypeBuilder<StateTransition> builder)
    {
        builder.ToTable("StateTransitions");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.MachineId).IsRequired();
        builder.Property(entity => entity.FromState).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ToState).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ActionCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ActionName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.RequiredPermission).HasMaxLength(200);
        builder.Property(entity => entity.ConditionJson).HasMaxLength(4000);
        builder.Property(entity => entity.IsEnabled).IsRequired().HasDefaultValue(true);
        builder.Property(entity => entity.Sort).IsRequired();

        builder.HasIndex(entity => new { entity.TenantId, entity.MachineId, entity.FromState, entity.ActionCode });
        builder.HasIndex(entity => new { entity.TenantId, entity.MachineId, entity.IsEnabled, entity.Sort });
    }
}
