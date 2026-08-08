using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class DemoBusinessOrderConfiguration : IEntityTypeConfiguration<DemoBusinessOrder>
{
    public void Configure(EntityTypeBuilder<DemoBusinessOrder> builder)
    {
        builder.ToTable("demo_business_order");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.OrderNo).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.CustomerName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Amount).HasPrecision(18, 2);
        builder.Property(entity => entity.OwnerUserName).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ApprovalStatus).IsRequired().HasDefaultValue(ApprovalStatus.Draft);
        builder.Property(entity => entity.ChangeHistoryJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();

        builder.HasIndex(entity => new { entity.TenantId, entity.OrderNo, entity.IsDeleted }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.ApprovalStatus, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.DepartmentId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.OwnerUserId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.WorkflowInstanceId });
    }
}
