using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class AiUsageLogConfiguration : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> builder)
    {
        builder.ToTable("ai_usage_log");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.ModelName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.ProviderRequestId).HasMaxLength(200);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.RouteRole)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(entity => entity.EstimatedCost).HasPrecision(18, 6);
        builder.Property(entity => entity.ReservedCost).HasPrecision(18, 6);
        builder.Property(entity => entity.InputTokenPricePerMillion).HasPrecision(18, 6);
        builder.Property(entity => entity.OutputTokenPricePerMillion).HasPrecision(18, 6);
        builder.Property(entity => entity.PricingCurrency).HasMaxLength(3);
        builder.Property(entity => entity.FinishReason).HasMaxLength(100);
        builder.Property(entity => entity.ErrorCode).HasMaxLength(100);

        builder.HasOne<AiRun>()
            .WithMany()
            .HasForeignKey(entity => entity.RunId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AiProviderConfig>()
            .WithMany()
            .HasForeignKey(entity => entity.ProviderConfigId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.RunId, entity.Sequence })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.ProviderConfigId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.Status, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.PricingCurrency, entity.CreatedAt });
    }
}
