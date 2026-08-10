using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class ReportQueryParamConfiguration : IEntityTypeConfiguration<ReportQueryParam>
{
    public void Configure(EntityTypeBuilder<ReportQueryParam> builder)
    {
        builder.ToTable("ReportQueryParams");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.ParamCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ParamName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.ParamType).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.DefaultValue).HasMaxLength(500);

        builder.HasIndex(entity => new { entity.TenantId, entity.ReportId, entity.ParamCode })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.ReportId, entity.Sort });
    }
}
