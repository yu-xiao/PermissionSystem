using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class AiToolInvocationConfiguration : IEntityTypeConfiguration<AiToolInvocation>
{
    public void Configure(EntityTypeBuilder<AiToolInvocation> builder)
    {
        builder.ToTable("ai_tool_invocation");
        builder.ConfigureBaseEntity();

        builder.Property(entity => entity.InvocationId).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.ToolCode).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.ToolVersion).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.InputDigest).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.OutputDigest).HasMaxLength(128);
        builder.Property(entity => entity.SourceSystem).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.DatasetCode).HasMaxLength(100);
        builder.Property(entity => entity.DatasetVersion).HasMaxLength(64);
        builder.Property(entity => entity.ErrorCode).HasMaxLength(100);
        builder.Property(entity => entity.CitationJson).HasColumnType("nvarchar(max)");

        builder.HasOne<AiRun>()
            .WithMany()
            .HasForeignKey(entity => entity.RunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.TenantId, entity.InvocationId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.RunId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.ToolCode, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.TenantId, entity.Status, entity.CreatedAt });
    }
}
