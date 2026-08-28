using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class AiProviderConfigConfiguration : IEntityTypeConfiguration<AiProviderConfig>
{
    public void Configure(EntityTypeBuilder<AiProviderConfig> builder)
    {
        builder.ToTable("ai_provider_config");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.ProviderCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ProviderName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.ProviderType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.BaseUrl).HasMaxLength(1000).IsRequired();
        builder.Property(entity => entity.ChatCompletionsPath).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.ApiKeyEncrypted).HasMaxLength(2000).IsRequired();
        builder.Property(entity => entity.ModelName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.TimeoutSeconds).HasDefaultValue(30);
        builder.Property(entity => entity.Temperature).HasPrecision(5, 4);
        builder.Property(entity => entity.AllowedHostsJson).HasMaxLength(4000).IsRequired();
        builder.Property(entity => entity.DataResidency).HasMaxLength(100);
        builder.Property(entity => entity.SupportsTools).HasDefaultValue(true);
        builder.Property(entity => entity.InputTokenPricePerMillion).HasPrecision(18, 6);
        builder.Property(entity => entity.OutputTokenPricePerMillion).HasPrecision(18, 6);
        builder.Property(entity => entity.PricingCurrency).HasMaxLength(3);
        builder.Property(entity => entity.Remark).HasMaxLength(500);

        builder.HasIndex(entity => new { entity.TenantId, entity.ProviderCode })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.IsDefault })
            .IsUnique()
            .HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.IsEnabled, entity.IsDefault });
    }
}
