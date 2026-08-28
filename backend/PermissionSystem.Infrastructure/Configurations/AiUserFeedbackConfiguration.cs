using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class AiUserFeedbackConfiguration : IEntityTypeConfiguration<AiUserFeedback>
{
    public void Configure(EntityTypeBuilder<AiUserFeedback> builder)
    {
        builder.ToTable("ai_user_feedback");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.Rating).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.ReasonCode).HasMaxLength(64);
        builder.Property(entity => entity.Comment).HasMaxLength(500);

        builder.HasOne<AiRun>()
            .WithMany()
            .HasForeignKey(entity => entity.RunId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AiMessage>()
            .WithMany()
            .HasForeignKey(entity => entity.MessageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.RunId, entity.UserId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.Rating, entity.CreatedAt });
    }
}
