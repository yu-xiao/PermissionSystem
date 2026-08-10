using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class PrintTemplateConfiguration : IEntityTypeConfiguration<PrintTemplate>
{
    public void Configure(EntityTypeBuilder<PrintTemplate> builder)
    {
        builder.ToTable("PrintTemplates");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.TemplateCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.TemplateName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.BusinessType).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.TemplateType).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.ContentHtml).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(entity => entity.ContentJson).HasColumnType("nvarchar(max)");
        builder.Property(entity => entity.PaperSize).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Orientation).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.IsDefault).IsRequired().HasDefaultValue(false);
        builder.Property(entity => entity.IsEnabled).IsRequired().HasDefaultValue(true);
        builder.Property(entity => entity.Version).IsRequired().HasDefaultValue(1);
        builder.Property(entity => entity.Remark).HasMaxLength(512);

        builder.HasIndex(entity => new { entity.TenantId, entity.TemplateCode })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.BusinessType, entity.TemplateType, entity.IsEnabled });
        builder.HasIndex(entity => new { entity.TenantId, entity.BusinessType, entity.IsDefault });
    }
}
