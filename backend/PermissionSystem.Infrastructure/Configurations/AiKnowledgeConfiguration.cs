using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Configurations;

public sealed class AiKnowledgeDocumentConfiguration : IEntityTypeConfiguration<AiKnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<AiKnowledgeDocument> b)
    {
        b.ToTable("ai_knowledge_document"); b.ConfigureBaseEntity();
        b.Property(d => d.Title).HasMaxLength(200).IsRequired();
        b.Property(d => d.Owner).HasMaxLength(200).IsRequired();
        b.Property(d => d.License).HasMaxLength(500).IsRequired();
        b.Property(d => d.Classification).HasMaxLength(32).IsRequired();
        b.HasAlternateKey(d => new { d.TenantId, d.Id });
        b.HasIndex(d => new { d.TenantId, d.IsDeleted, d.CreatedAt });
        b.HasOne<AiKnowledgeDocumentVersion>().WithMany().HasForeignKey(d => new { d.TenantId, DocumentId = d.Id, d.CurrentVersionId })
            .HasPrincipalKey(v => new { v.TenantId, v.DocumentId, v.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AiKnowledgeDocumentVersionConfiguration : IEntityTypeConfiguration<AiKnowledgeDocumentVersion>
{
    public void Configure(EntityTypeBuilder<AiKnowledgeDocumentVersion> b)
    {
        b.ToTable("ai_knowledge_document_version", t => t.HasCheckConstraint("CK_ai_knowledge_version_Validity", "[ValidUntil] > [ValidFrom]"));
        b.ConfigureBaseEntity();
        b.Property(v => v.ContentHash).HasMaxLength(64).IsRequired();
        b.Property(v => v.ParserVersion).HasMaxLength(64).IsRequired();
        b.Property(v => v.ParseStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(v => v.ErrorCode).HasMaxLength(100);
        b.HasAlternateKey(v => new { v.TenantId, v.DocumentId, v.Id });
        b.HasIndex(v => new { v.TenantId, v.DocumentId, v.VersionNumber }).IsUnique();
        b.HasIndex(v => new { v.TenantId, v.DocumentId, v.ContentHash }).IsUnique();
        b.HasOne<AiKnowledgeDocument>().WithMany().HasForeignKey(v => new { v.TenantId, v.DocumentId })
            .HasPrincipalKey(d => new { d.TenantId, d.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FileResource>().WithMany().HasForeignKey(v => new { v.TenantId, v.FileResourceId })
            .HasPrincipalKey(f => new { f.TenantId, f.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AiKnowledgeChunkConfiguration : IEntityTypeConfiguration<AiKnowledgeChunk>
{
    public void Configure(EntityTypeBuilder<AiKnowledgeChunk> b)
    {
        b.ToTable("ai_knowledge_chunk"); b.ConfigureBaseEntity();
        b.Property(c => c.Content).HasMaxLength(2000).IsRequired();
        b.Property(c => c.ContentHash).HasMaxLength(64).IsRequired();
        b.HasAlternateKey(c => new { c.TenantId, c.DocumentId, c.VersionId, c.Id });
        b.HasIndex(c => new { c.TenantId, c.VersionId, c.Sequence }).IsUnique();
        b.HasOne<AiKnowledgeDocumentVersion>().WithMany().HasForeignKey(c => new { c.TenantId, c.DocumentId, c.VersionId })
            .HasPrincipalKey(v => new { v.TenantId, v.DocumentId, v.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AiKnowledgeDocumentRoleConfiguration : IEntityTypeConfiguration<AiKnowledgeDocumentRole>
{
    public void Configure(EntityTypeBuilder<AiKnowledgeDocumentRole> b)
    {
        b.ToTable("ai_knowledge_document_role"); b.ConfigureBaseEntity();
        b.HasIndex(g => new { g.TenantId, g.DocumentId, g.RoleId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne<AiKnowledgeDocument>().WithMany().HasForeignKey(g => new { g.TenantId, g.DocumentId })
            .HasPrincipalKey(d => new { d.TenantId, d.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Role>().WithMany().HasForeignKey(g => new { g.TenantId, g.RoleId })
            .HasPrincipalKey(r => new { r.TenantId, r.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AiKnowledgeRunReferenceConfiguration : IEntityTypeConfiguration<AiKnowledgeRunReference>
{
    public void Configure(EntityTypeBuilder<AiKnowledgeRunReference> b)
    {
        b.ToTable("ai_knowledge_run_reference"); b.ConfigureBaseEntity();
        b.Property(r => r.InvocationId).HasMaxLength(100).IsRequired();
        b.Property(r => r.ContentHash).HasMaxLength(64).IsRequired();
        b.HasIndex(r => new { r.TenantId, r.RunId, r.InvocationId, r.ChunkId }).IsUnique();
        b.HasIndex(r => new { r.TenantId, r.DocumentId, r.RunId });
        b.HasOne<AiKnowledgeChunk>().WithMany().HasForeignKey(r => new { r.TenantId, r.DocumentId, r.VersionId, r.ChunkId })
            .HasPrincipalKey(c => new { c.TenantId, c.DocumentId, c.VersionId, c.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AiRun>().WithMany().HasForeignKey(r => new { r.TenantId, r.RunId })
            .HasPrincipalKey(r => new { r.TenantId, r.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
