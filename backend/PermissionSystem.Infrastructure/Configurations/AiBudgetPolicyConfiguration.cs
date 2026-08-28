using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class AiBudgetPolicyConfiguration : IEntityTypeConfiguration<AiBudgetPolicy>
{
    public void Configure(EntityTypeBuilder<AiBudgetPolicy> builder)
    {
        builder.ToTable("ai_budget_policy");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.PolicyCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.PolicyName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.ScopeType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.MonthlyLimit).HasPrecision(18, 6);
        builder.Property(entity => entity.Currency).HasMaxLength(3).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.PolicyCode })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.ScopeType, entity.UserId, entity.IsEnabled });
        builder.HasIndex(entity => new { entity.TenantId, entity.ScopeType, entity.UserId, entity.Currency })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
