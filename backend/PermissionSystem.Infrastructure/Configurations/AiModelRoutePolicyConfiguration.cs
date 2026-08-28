using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class AiModelRoutePolicyConfiguration : IEntityTypeConfiguration<AiModelRoutePolicy>
{
    public void Configure(EntityTypeBuilder<AiModelRoutePolicy> builder)
    {
        builder.ToTable("ai_model_route_policy");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.AgentCode).HasMaxLength(100).IsRequired();

        builder.HasOne<AiProviderConfig>()
            .WithMany()
            .HasForeignKey(entity => entity.PrimaryProviderConfigId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AiProviderConfig>()
            .WithMany()
            .HasForeignKey(entity => entity.CanaryProviderConfigId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AiProviderConfig>()
            .WithMany()
            .HasForeignKey(entity => entity.FallbackProviderConfigId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.AgentCode })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.IsEnabled });
    }
}
