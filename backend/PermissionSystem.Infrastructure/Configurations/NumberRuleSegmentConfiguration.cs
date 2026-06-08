using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class NumberRuleSegmentConfiguration : IEntityTypeConfiguration<NumberRuleSegment>
{
    public void Configure(EntityTypeBuilder<NumberRuleSegment> builder)
    {
        builder.ToTable("NumberRuleSegments");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.RuleId).IsRequired();
        builder.Property(entity => entity.SegmentType).IsRequired();
        builder.Property(entity => entity.SegmentValue).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.Sort).IsRequired();

        builder.HasIndex(entity => new { entity.TenantId, entity.RuleId, entity.Sort });
    }
}
