using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class StateMachineDefinitionConfiguration : IEntityTypeConfiguration<StateMachineDefinition>
{
    public void Configure(EntityTypeBuilder<StateMachineDefinition> builder)
    {
        builder.ToTable("StateMachineDefinitions");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.BusinessType).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.Property(entity => entity.IsEnabled).IsRequired().HasDefaultValue(true);

        builder.HasIndex(entity => new { entity.TenantId, entity.BusinessType })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.IsEnabled });
    }
}
