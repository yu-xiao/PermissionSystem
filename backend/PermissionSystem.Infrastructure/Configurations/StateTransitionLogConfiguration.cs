using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class StateTransitionLogConfiguration : IEntityTypeConfiguration<StateTransitionLog>
{
    public void Configure(EntityTypeBuilder<StateTransitionLog> builder)
    {
        builder.ToTable("StateTransitionLogs");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.BusinessType).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.BusinessId).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.FromState).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ToState).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ActionCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ActionName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.OperatorUserName).HasMaxLength(100);
        builder.Property(entity => entity.Comment).HasMaxLength(1000);

        builder.HasIndex(entity => new { entity.TenantId, entity.BusinessType, entity.BusinessId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.BusinessType, entity.ActionCode, entity.CreatedAt });
    }
}
