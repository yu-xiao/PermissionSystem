using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.UserName).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.SessionId).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.AccessTokenId).HasMaxLength(128);
        builder.Property(entity => entity.RefreshTokenId).HasMaxLength(128);
        builder.Property(entity => entity.IpAddress).HasMaxLength(64);
        builder.Property(entity => entity.UserAgent).HasMaxLength(512);
        builder.Property(entity => entity.RevokedReason).HasMaxLength(512);
        builder.Property(entity => entity.LoginAt).IsRequired();
        builder.Property(entity => entity.LastActiveAt).IsRequired();
        builder.Property(entity => entity.ExpiresAt).IsRequired();

        builder.HasIndex(entity => new { entity.TenantId, entity.UserId, entity.IsRevoked, entity.LastActiveAt });
        builder.HasIndex(entity => entity.SessionId).IsUnique();
        builder.HasIndex(entity => entity.RefreshTokenId);
    }
}
