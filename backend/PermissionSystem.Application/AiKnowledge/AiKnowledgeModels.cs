using PermissionSystem.Application.Files;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.AiKnowledge;

public static class AiKnowledgeContract
{
    public const string BusinessType = "AiKnowledgeDocument";
    public const string ToolCode = "knowledge.documents.search";
    public const int MaxFileBytes = 256 * 1024;
    public const int MaxDocuments = 100;
    public const int MaxChunks = 256;
    public const int MaxChunkCharacters = 2000;
    public const int MaxResults = 5;
    public const string Unavailable = "来源已不可用，请重新查询。";
    public const string Limitation = "仅支持合成文本的直接词匹配；文档资料不代表实时业务状态。";
}

public sealed class CreateAiKnowledgeDocumentRequest
{
    public string Title { get; init; } = string.Empty;
    public string Owner { get; init; } = string.Empty;
    public string License { get; init; } = string.Empty;
    public bool Synthetic { get; init; }
    public Guid[] RoleIds { get; init; } = [];
}

public sealed class AiKnowledgeAccessRequest
{
    public Guid[] RoleIds { get; init; } = [];
    public byte[] RowVersion { get; init; } = [];
}

public sealed class AiKnowledgePublishRequest
{
    public byte[] RowVersion { get; init; } = [];
}

public sealed class AiKnowledgeUploadRequest
{
    public required UploadFileRequest File { get; init; }
    public DateTimeOffset ValidFrom { get; init; }
    public DateTimeOffset ValidUntil { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed class AiKnowledgeSearchRequest
{
    public string Keyword { get; init; } = string.Empty;
    public Guid? DocumentId { get; init; }
    public int Limit { get; init; } = AiKnowledgeContract.MaxResults;
}

public sealed record AiKnowledgeVersionResponse(Guid Id, int VersionNumber, string ParseStatus,
    string? ErrorCode, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil, DateTimeOffset? PublishedAt);

public sealed record AiKnowledgeDocumentResponse(Guid Id, string Title, string Owner, string License,
    Guid? CurrentVersionId, int AccessVersion, byte[] RowVersion, IReadOnlyList<Guid> RoleIds,
    IReadOnlyList<AiKnowledgeVersionResponse> Versions);

public sealed record AiKnowledgeReference(Guid DocumentId, Guid VersionId, Guid ChunkId, string ContentHash);

public sealed record AiKnowledgeHit(AiKnowledgeReference Reference, string Title, int VersionNumber,
    int StartLine, int EndLine, string Content, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil);

public sealed record AiKnowledgeSearchResult(IReadOnlyList<AiKnowledgeHit> Items, DateTimeOffset QueriedAt,
    bool IsTruncated, string Limitation = AiKnowledgeContract.Limitation);

public interface IAiKnowledgeService
{
    Task<PagedResult<AiKnowledgeDocumentResponse>> ListAsync(int pageIndex, int pageSize, CancellationToken ct = default);
    Task<AiKnowledgeDocumentResponse> CreateAsync(CreateAiKnowledgeDocumentRequest request, CancellationToken ct = default);
    Task<AiKnowledgeDocumentResponse> SetAccessAsync(Guid id, AiKnowledgeAccessRequest request, CancellationToken ct = default);
    Task<AiKnowledgeDocumentResponse> UploadAsync(Guid id, AiKnowledgeUploadRequest request, CancellationToken ct = default);
    Task PublishAsync(Guid id, Guid versionId, AiKnowledgePublishRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, byte[] rowVersion, CancellationToken ct = default);
    Task<AiKnowledgeSearchResult> SearchAsync(AiKnowledgeSearchRequest request, CancellationToken ct = default);
    Task<AiKnowledgeHit> ReadChunkAsync(AiKnowledgeReference reference, CancellationToken ct = default);
    Task<AiKnowledgeSearchResult> PreviewAsync(Guid id, Guid versionId, int startSequence = 1, CancellationToken ct = default);
}
