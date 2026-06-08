using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class SecurityPolicyConfiguration : IEntityTypeConfiguration<SecurityPolicy>
{
    public void Configure(EntityTypeBuilder<SecurityPolicy> builder)
    {
        builder.ToTable("SecurityPolicies");

        builder.Property(entity => entity.PasswordMinLength).HasDefaultValue(8);
        builder.Property(entity => entity.RequireDigit).HasDefaultValue(true);
        builder.Property(entity => entity.RequireLowercase).HasDefaultValue(true);
        builder.Property(entity => entity.LoginFailureLockThreshold).HasDefaultValue(5);
        builder.Property(entity => entity.LoginFailureLockMinutes).HasDefaultValue(15);

        builder.HasIndex(entity => entity.TenantId).IsUnique();
    }
}
