using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class NumberRuleConfiguration : IEntityTypeConfiguration<NumberRule>
{
    public void Configure(EntityTypeBuilder<NumberRule> builder)
    {
        builder.ToTable("NumberRules");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.RuleCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.RuleName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.BusinessType).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Prefix).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.DateFormat).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.SequenceLength).IsRequired().HasDefaultValue(4);
        builder.Property(entity => entity.ResetCycle)
            .IsRequired()
            .HasDefaultValue(NumberRuleResetCycle.Daily)
            .HasSentinel((NumberRuleResetCycle)(-1));
        builder.Property(entity => entity.Separator).HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.IsEnabled).IsRequired().HasDefaultValue(true);
        builder.Property(entity => entity.Remark).HasMaxLength(512);

        builder.HasIndex(entity => new { entity.TenantId, entity.RuleCode })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.BusinessType, entity.IsEnabled });
    }
}
