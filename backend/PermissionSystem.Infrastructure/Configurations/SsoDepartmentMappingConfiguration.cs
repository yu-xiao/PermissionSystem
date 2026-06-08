using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class SsoDepartmentMappingConfiguration : IEntityTypeConfiguration<SsoDepartmentMapping>
{
    public void Configure(EntityTypeBuilder<SsoDepartmentMapping> builder)
    {
        builder.ToTable("sso_department_mapping");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.ExternalDepartment).HasMaxLength(256).IsRequired();

        builder.HasOne(entity => entity.Provider)
            .WithMany(entity => entity.DepartmentMappings)
            .HasForeignKey(entity => entity.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.LocalDepartment)
            .WithMany()
            .HasForeignKey(entity => entity.LocalDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.ProviderId, entity.ExternalDepartment, entity.LocalDepartmentId }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.ProviderId });
        builder.HasIndex(entity => entity.LocalDepartmentId);
    }
}
