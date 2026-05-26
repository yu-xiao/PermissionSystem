using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class DemoApprovalOrderConfiguration : IEntityTypeConfiguration<DemoApprovalOrder>
{
    public void Configure(EntityTypeBuilder<DemoApprovalOrder> builder)
    {
        builder.ToTable("demo_approval_order");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.OrderNo).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Amount).HasPrecision(18, 2);
        builder.Property(entity => entity.ApplicantUserName).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ApprovalStatus).IsRequired().HasDefaultValue(ApprovalStatus.Draft);

        builder.HasIndex(entity => new { entity.TenantId, entity.OrderNo, entity.IsDeleted }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.ApprovalStatus, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.WorkflowInstanceId });
    }
}
