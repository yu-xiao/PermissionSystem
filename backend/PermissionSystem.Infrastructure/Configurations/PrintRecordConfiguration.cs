using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class PrintRecordConfiguration : IEntityTypeConfiguration<PrintRecord>
{
    public void Configure(EntityTypeBuilder<PrintRecord> builder)
    {
        builder.ToTable("PrintRecords");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.TemplateId).IsRequired();
        builder.Property(entity => entity.BusinessType).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.BusinessId).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.PrintUserName).HasMaxLength(100);
        builder.Property(entity => entity.PrintedAt).IsRequired();
        builder.Property(entity => entity.PrintCount).IsRequired().HasDefaultValue(1);

        builder.HasIndex(entity => new { entity.TenantId, entity.TemplateId, entity.PrintedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.BusinessType, entity.BusinessId, entity.PrintedAt });
    }
}
