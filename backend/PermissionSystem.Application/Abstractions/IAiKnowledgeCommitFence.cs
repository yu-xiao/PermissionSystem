namespace PermissionSystem.Application.Abstractions;

public interface IAiKnowledgeCommitFence
{
    Task HoldAsync(Guid runId, CancellationToken ct);
    Task HoldDocumentAsync(Guid documentId, CancellationToken ct);
    Task HoldCapacityAsync(Guid tenantId, CancellationToken ct);
    Task HoldPublicationAsync(Guid documentId, Guid versionId, CancellationToken ct);
}
