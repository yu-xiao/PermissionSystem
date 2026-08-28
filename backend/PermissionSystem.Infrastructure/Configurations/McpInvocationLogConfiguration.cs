using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class McpInvocationLogConfiguration : IEntityTypeConfiguration<McpInvocationLog>
{
    public void Configure(EntityTypeBuilder<McpInvocationLog> builder)
    {
        builder.ToTable("mcp_invocation_log");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.CallerType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.OAuthClientId).HasMaxLength(100);
        builder.Property(entity => entity.ToolName).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.DatasetCode).HasMaxLength(100);
        builder.Property(entity => entity.TraceId).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.InputDigest).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.IpAddress).HasMaxLength(64);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.ErrorCode).HasMaxLength(100);
        builder.Property(entity => entity.ErrorSummary).HasMaxLength(1000);

        builder.HasOne<McpClientBinding>()
            .WithMany()
            .HasForeignKey(entity => entity.ClientBindingId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.ClientBindingId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.Status, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.DatasetCode, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.TraceId });
    }
}
