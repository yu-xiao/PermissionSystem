using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class SensitiveOperationVerificationConfiguration : IEntityTypeConfiguration<SensitiveOperationVerification>
{
    public void Configure(EntityTypeBuilder<SensitiveOperationVerification> builder)
    {
        builder.ToTable("SensitiveOperationVerifications");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.SessionId).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.OperationCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.VerificationMethod).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.TicketHash).HasMaxLength(64);
        builder.HasIndex(entity => new { entity.TenantId, entity.UserId, entity.SessionId, entity.OperationCode, entity.ExpiresAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.TicketHash })
            .IsUnique()
            .HasFilter("[TicketHash] IS NOT NULL");
    }
}
