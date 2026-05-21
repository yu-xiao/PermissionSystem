using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.MessageId).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Consumer).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.MessageType).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.PayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Status).HasMaxLength(32).IsRequired();

        builder.HasIndex(entity => new { entity.TenantId, entity.MessageId, entity.Consumer }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.Consumer, entity.Status, entity.CreatedAt });
    }
}
