using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class AiMessageConfiguration : IEntityTypeConfiguration<AiMessage>
{
    public void Configure(EntityTypeBuilder<AiMessage> builder)
    {
        builder.ToTable("ai_message");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Content).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(entity => entity.ContentClassification).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.ContentDigest).HasMaxLength(128).IsRequired();

        builder.HasOne<AiConversation>()
            .WithMany()
            .HasForeignKey(entity => entity.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.ConversationId, entity.Sequence })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
