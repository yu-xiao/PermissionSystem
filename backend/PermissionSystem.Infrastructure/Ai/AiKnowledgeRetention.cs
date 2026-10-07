using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.AiKnowledge;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Data;

namespace PermissionSystem.Infrastructure.Ai;

public static class AiKnowledgeRetention
{
    public static async Task CleanupExpiredAsync(AppDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        // Explicit synthetic validity is the cleanup boundary; real-document retention is not defined yet.
        var expired = from version in db.AiKnowledgeDocumentVersions.IgnoreQueryFilters()
                      join document in db.AiKnowledgeDocuments.IgnoreQueryFilters()
                          on new { version.TenantId, Id = version.DocumentId } equals new { document.TenantId, document.Id }
                      where document.Classification == "Synthetic" && version.ValidUntil <= now
                      select version;
        await db.AiKnowledgeChunks.IgnoreQueryFilters()
            .Where(chunk => chunk.Content != "" && expired.Any(v => v.TenantId == chunk.TenantId &&
                v.DocumentId == chunk.DocumentId && v.Id == chunk.VersionId))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Content, ""), ct);
        await db.FileResources.IgnoreQueryFilters()
            .Where(file => file.BusinessType == AiKnowledgeContract.BusinessType &&
                file.FileStatus != FileStatus.Deleted && file.FileStatus != FileStatus.PendingDelete &&
                (expired.Any(v => v.TenantId == file.TenantId && v.DocumentId == file.BusinessId && v.FileResourceId == file.Id) ||
                 db.AiKnowledgeDocuments.IgnoreQueryFilters().Any(d => d.TenantId == file.TenantId &&
                    d.Id == file.BusinessId && d.Classification == "Synthetic" && (d.IsDeleted ||
                    (db.AiKnowledgeDocumentVersions.IgnoreQueryFilters().Any(v => v.TenantId == d.TenantId && v.DocumentId == d.Id) &&
                     !db.AiKnowledgeDocumentVersions.IgnoreQueryFilters().Any(v => v.TenantId == d.TenantId && v.DocumentId == d.Id && v.ValidUntil > now))))))
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.FileStatus, FileStatus.PendingDelete)
                .SetProperty(f => f.NextRetryAt, (DateTimeOffset?)null).SetProperty(f => f.LastError, (string?)null), ct);
    }
}
