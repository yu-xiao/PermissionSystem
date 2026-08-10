using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class IpAccessRuleConfiguration : IEntityTypeConfiguration<IpAccessRule>
{
    public void Configure(EntityTypeBuilder<IpAccessRule> builder)
    {
        builder.ToTable("IpAccessRules");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.RuleType).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.IpPattern).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(500);

        builder.HasIndex(entity => new { entity.TenantId, entity.RuleType, entity.IpPattern })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.RuleType, entity.IsEnabled });
    }
}
