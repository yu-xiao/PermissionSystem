using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.UserName).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.NormalizedUserName).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Email).HasMaxLength(256);
        builder.Property(entity => entity.PhoneNumber).HasMaxLength(32);
        builder.Property(entity => entity.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.DisplayName).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.AvatarUrl).HasMaxLength(512);
        builder.Property(entity => entity.IsEnabled).IsRequired().HasDefaultValue(true);
        builder.Property(entity => entity.IsBuiltin).IsRequired().HasDefaultValue(false);

        builder.HasOne(entity => entity.Department)
            .WithMany(entity => entity.Users)
            .HasForeignKey(entity => entity.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.NormalizedUserName }).IsUnique();
    }
}
