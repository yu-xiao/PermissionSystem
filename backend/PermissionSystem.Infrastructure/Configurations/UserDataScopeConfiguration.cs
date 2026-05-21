using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class UserDataScopeConfiguration : IEntityTypeConfiguration<UserDataScope>
{
    public void Configure(EntityTypeBuilder<UserDataScope> builder)
    {
        builder.ToTable("UserDataScopes");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.ScopeType).IsRequired();
        builder.Property(entity => entity.CustomDepartmentIds).HasMaxLength(2000);

        builder.HasOne(entity => entity.User)
            .WithOne(entity => entity.DataScope)
            .HasForeignKey<UserDataScope>(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.UserId }).IsUnique();
    }
}
