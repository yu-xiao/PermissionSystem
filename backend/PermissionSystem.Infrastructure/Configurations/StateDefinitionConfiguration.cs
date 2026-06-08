using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class StateDefinitionConfiguration : IEntityTypeConfiguration<StateDefinition>
{
    public void Configure(EntityTypeBuilder<StateDefinition> builder)
    {
        builder.ToTable("StateDefinitions");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.MachineId).IsRequired();
        builder.Property(entity => entity.StateCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.StateName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.StateType).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Color).HasMaxLength(32);
        builder.Property(entity => entity.Sort).IsRequired();
        builder.Property(entity => entity.IsInitial).IsRequired().HasDefaultValue(false);
        builder.Property(entity => entity.IsFinal).IsRequired().HasDefaultValue(false);

        builder.HasIndex(entity => new { entity.TenantId, entity.MachineId, entity.StateCode }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.MachineId, entity.Sort });
    }
}
