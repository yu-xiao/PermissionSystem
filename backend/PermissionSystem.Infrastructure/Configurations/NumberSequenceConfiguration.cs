using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> builder)
    {
        builder.ToTable("NumberSequences");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.RuleCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.SequenceKey).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.CurrentValue).IsRequired();
        builder.Property(entity => entity.LastGeneratedAt);

        builder.HasIndex(entity => new { entity.TenantId, entity.RuleCode, entity.SequenceKey }).IsUnique();
    }
}
