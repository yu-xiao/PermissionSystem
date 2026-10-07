using System.Security.Cryptography;
using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Files;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.AiKnowledge;

public sealed class AiKnowledgeService(
    AiKnowledgeAccessPolicy access, IRepository<AiKnowledgeDocument> documents,
    IRepository<AiKnowledgeDocumentVersion> versions, IRepository<AiKnowledgeChunk> chunks,
    IRepository<AiKnowledgeDocumentRole> grants, IRepository<Role> roles, IRepository<FileResource> files,
    IRepository<AiKnowledgeRunReference> references, IRepository<AiRun> runs,
    IRepository<AiMessage> messages, IRepository<AiToolInvocation> invocations,
    IFileService fileService, IAiKnowledgeTextParser parser, IAsyncQueryExecutor queries,
    IUnitOfWork unit, IDistributedLock locks, IAiKnowledgeCommitFence? mutationFence = null) : IAiKnowledgeService
{
    public async Task<PagedResult<AiKnowledgeDocumentResponse>> ListAsync(int pageIndex, int pageSize, CancellationToken ct = default)
    {
        var actor = await access.AuthorizeAsync(AiCenterConstants.KnowledgeViewPermission, ct);
        if (pageIndex < 1 || pageSize is < 1 or > 50) throw Invalid("Invalid knowledge pagination.");
        var query = documents.Query().Where(d => !d.IsDeleted && d.TenantId == actor.TenantId);
        var count = await queries.LongCountAsync(query, ct);
        var items = await queries.ToListAsync(query.OrderByDescending(d => d.CreatedAt).ThenBy(d => d.Id)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize), ct);
        var responses = new List<AiKnowledgeDocumentResponse>();
        foreach (var item in items) responses.Add(await ToResponseAsync(item, ct));
        return PagedResult<AiKnowledgeDocumentResponse>.Create(responses, pageIndex, pageSize, count);
    }

    public async Task<AiKnowledgeDocumentResponse> CreateAsync(CreateAiKnowledgeDocumentRequest request, CancellationToken ct = default)
    {
        var actor = await access.AuthorizeAsync(AiCenterConstants.KnowledgeManagePermission, ct);
        if (!request.Synthetic) throw Invalid("Only explicitly synthetic documents are supported in this batch.");
        var title = Required(request.Title, 200);
        var owner = Required(request.Owner, 200);
        var license = Required(request.License, 500);
        var roleIds = await ValidateRolesAsync(actor.TenantId, request.RoleIds, ct);
        return await locks.ExecuteWithLockAsync($"ai:knowledge:create:{actor.TenantId:N}", async token =>
        {
            var document = new AiKnowledgeDocument { Id = Guid.NewGuid(), TenantId = actor.TenantId,
                Title = title, Owner = owner, License = license };
            await unit.ExecuteInTransactionAsync(async transactionToken =>
            {
                if (mutationFence is not null) await mutationFence.HoldCapacityAsync(actor.TenantId, transactionToken);
                if (await queries.LongCountAsync(documents.Query().Where(d => d.TenantId == actor.TenantId && !d.IsDeleted), transactionToken) >= AiKnowledgeContract.MaxDocuments)
                    throw Invalid("The synthetic document limit has been reached.");
                await documents.AddAsync(document, transactionToken);
                foreach (var roleId in roleIds) await grants.AddAsync(new AiKnowledgeDocumentRole
                    { Id = Guid.NewGuid(), TenantId = actor.TenantId, DocumentId = document.Id, RoleId = roleId }, transactionToken);
                await unit.SaveChangesAsync(transactionToken);
            }, token);
            return await ToResponseAsync(document, token);
        }, cancellationToken: ct);
    }

    public async Task<AiKnowledgeDocumentResponse> SetAccessAsync(Guid id, AiKnowledgeAccessRequest request, CancellationToken ct = default)
    {
        var actor = await access.AuthorizeAsync(AiCenterConstants.KnowledgeManagePermission, ct);
        var document = await GetDocumentAsync(id, actor.TenantId, ct);
        var roleIds = await ValidateRolesAsync(actor.TenantId, request.RoleIds, ct);
        await unit.ExecuteInTransactionAsync(async token =>
        {
            await CheckTokenAsync(document, request.RowVersion, token, forWrite: true);
            var existing = await queries.ToListAsync(grants.Query().Where(g => g.TenantId == actor.TenantId && g.DocumentId == id), token);
            foreach (var grant in existing.Where(g => !roleIds.Contains(g.RoleId))) grants.Remove(grant);
            foreach (var roleId in roleIds.Except(existing.Select(g => g.RoleId)))
                await grants.AddAsync(new AiKnowledgeDocumentRole { Id = Guid.NewGuid(), TenantId = actor.TenantId, DocumentId = id, RoleId = roleId }, token);
            document.AccessVersion++;
            documents.Update(document);
            await unit.SaveChangesAsync(token);
        }, ct);
        return await ToResponseAsync(document, ct);
    }

    public async Task<AiKnowledgeDocumentResponse> UploadAsync(Guid id, AiKnowledgeUploadRequest request, CancellationToken ct = default)
    {
        var actor = await access.AuthorizeAsync(AiCenterConstants.KnowledgeManagePermission, ct);
        var document = await GetDocumentAsync(id, actor.TenantId, ct);
        await CheckTokenAsync(document, request.RowVersion, ct);
        if (request.ValidUntil <= request.ValidFrom || request.ValidUntil <= DateTimeOffset.UtcNow ||
            !string.Equals(Path.GetExtension(request.File.OriginalName), ".txt", StringComparison.OrdinalIgnoreCase) ||
            request.File.Size is <= 0 or > AiKnowledgeContract.MaxFileBytes)
            throw Invalid("A bounded .txt file and an explicit valid period are required.");
        using var content = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await request.File.Content.ReadAsync(buffer, ct)) > 0)
        {
            if (content.Length + count > AiKnowledgeContract.MaxFileBytes) throw Invalid("Knowledge file exceeds the limit.");
            await content.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        if (content.Length != request.File.Size) throw Invalid("Knowledge file length does not match.");
        content.Position = 0;
        var parsed = await parser.ParseAsync(content, ct);
        var hash = Convert.ToHexString(SHA256.HashData(content.ToArray()));
        var version = await queries.FirstOrDefaultAsync(versions.Query().Where(v => !v.IsDeleted && v.TenantId == actor.TenantId &&
            v.DocumentId == id && v.ContentHash == hash), ct);
        if (version?.ParseStatus == AiKnowledgeParseStatus.Ready) return await ToResponseAsync(document, ct);
        await unit.ExecuteInTransactionAsync(async token =>
        {
            await CheckTokenAsync(document, request.RowVersion, token, forWrite: true);
            var newVersion = version is null;
            if (version is null)
            {
                version = new AiKnowledgeDocumentVersion { Id = Guid.NewGuid(), TenantId = actor.TenantId, DocumentId = id,
                    VersionNumber = ++document.LastVersionNumber, ContentHash = hash };
                await versions.AddAsync(version, token);
            }
            version.ParseStatus = AiKnowledgeParseStatus.Pending;
            version.ErrorCode = null;
            version.ValidFrom = request.ValidFrom;
            version.ValidUntil = request.ValidUntil;
            if (!newVersion) versions.Update(version);
            document.AccessVersion++;
            documents.Update(document);
            await unit.SaveChangesAsync(token);
        }, ct);
        var reservedToken = Token(document);
        {
            var existingFile = version!.FileResourceId.HasValue ? await files.GetByIdAsync(version.FileResourceId.Value, ct) : null;
            if (existingFile is null || existingFile.FileStatus != FileStatus.Active || existingFile.ScanStatus != FileScanStatus.Clean)
            {
                if (existingFile is not null)
                {
                    existingFile.FileStatus = FileStatus.PendingDelete;
                    existingFile.NextRetryAt = null;
                    files.Update(existingFile);
                    await unit.SaveChangesAsync(ct);
                }
                content.Position = 0;
                try
                {
                    var uploaded = await fileService.UploadAsync(new UploadFileRequest { Content = content, Size = content.Length,
                        OriginalName = request.File.OriginalName, ContentType = request.File.ContentType,
                        BusinessType = AiKnowledgeContract.BusinessType, BusinessId = id }, ct);
                    version.FileResourceId = uploaded.Id;
                }
                catch (Exception error) when (error is BusinessException or IOException or UnauthorizedAccessException)
                {
                    var failedFile = await queries.FirstOrDefaultAsync(files.Query().Where(f => f.TenantId == actor.TenantId &&
                        f.BusinessType == AiKnowledgeContract.BusinessType && f.BusinessId == id && f.Sha256 == hash.ToLowerInvariant())
                        .OrderByDescending(f => f.CreatedAt), CancellationToken.None);
                    version.FileResourceId = failedFile?.Id;
                    version.ParseStatus = AiKnowledgeParseStatus.Failed;
                    version.ErrorCode = "knowledge_import_failed";
                    versions.Update(version);
                    await unit.SaveChangesAsync(CancellationToken.None);
                    throw Invalid("Knowledge import failed; retry the synthetic version or replace the file.");
                }
            }
            await unit.ExecuteInTransactionAsync(async token =>
            {
                await access.AuthorizeAsync(AiCenterConstants.KnowledgeManagePermission, token);
                await CheckTokenAsync(document, reservedToken, token, forWrite: true);
                versions.Update(version);
                await unit.SaveChangesAsync(token);
            }, ct);
            await unit.ExecuteInTransactionAsync(async token =>
            {
                await access.AuthorizeAsync(AiCenterConstants.KnowledgeManagePermission, token);
                await CheckTokenAsync(document, reservedToken, token, forWrite: true);
                foreach (var item in parsed) await chunks.AddAsync(new AiKnowledgeChunk { Id = Guid.NewGuid(), TenantId = actor.TenantId,
                    DocumentId = id, VersionId = version.Id, Sequence = item.Sequence, StartLine = item.StartLine,
                    EndLine = item.EndLine, Content = item.Content, ContentHash = item.ContentHash }, token);
                version.ParseStatus = AiKnowledgeParseStatus.Ready;
                versions.Update(version);
                document.AccessVersion++;
                documents.Update(document);
                await unit.SaveChangesAsync(token);
            }, ct);
        }
        return await ToResponseAsync(document, ct);
    }

    public async Task PublishAsync(Guid id, Guid versionId, AiKnowledgePublishRequest request, CancellationToken ct = default)
    {
        var actor = await access.AuthorizeAsync(AiCenterConstants.KnowledgeManagePermission, ct);
        var document = await GetDocumentAsync(id, actor.TenantId, ct);
        var version = await queries.FirstOrDefaultAsync(versions.Query().Where(v => !v.IsDeleted && v.TenantId == actor.TenantId && v.DocumentId == id && v.Id == versionId), ct)
            ?? throw Unavailable();
        await unit.ExecuteInTransactionAsync(async token =>
        {
            await CheckTokenAsync(document, request.RowVersion, token, forWrite: true);
            if (mutationFence is not null) await mutationFence.HoldPublicationAsync(id, versionId, token);
            await access.AuthorizeAsync(AiCenterConstants.KnowledgeManagePermission, token);
            var reviewer = await access.AuthorizeAsync(AiCenterConstants.KnowledgeQueryPermission, token);
            if (!await queries.AnyAsync(access.ReviewableVersions(reviewer, DateTimeOffset.UtcNow)
                .Where(v => v.DocumentId == id && v.Id == versionId), token)) throw Unavailable();
            if (!await queries.AnyAsync(chunks.Query().Where(c => !c.IsDeleted && c.TenantId == actor.TenantId &&
                c.DocumentId == id && c.VersionId == versionId && c.Content != ""), token))
                throw Invalid("The knowledge version is not ready for publication.");
            document.CurrentVersionId = versionId;
            document.AccessVersion++;
            version.PublishedAt = DateTimeOffset.UtcNow;
            documents.Update(document);
            versions.Update(version);
            await unit.SaveChangesAsync(token);
        }, ct);
    }

    public async Task DeleteAsync(Guid id, byte[] rowVersion, CancellationToken ct = default)
    {
        var actor = await access.AuthorizeAsync(AiCenterConstants.KnowledgeManagePermission, ct);
        var document = await GetDocumentAsync(id, actor.TenantId, ct);
        await unit.ExecuteInTransactionAsync(async token =>
        {
            await CheckTokenAsync(document, rowVersion, token, forWrite: true);
            document.CurrentVersionId = null;
            document.AccessVersion++;
            document.IsDeleted = true;
            documents.Update(document);
            foreach (var chunk in await queries.ToListAsync(chunks.Query().Where(c => c.TenantId == actor.TenantId && c.DocumentId == id), token))
            { chunk.Content = ""; chunk.IsDeleted = true; chunks.Update(chunk); }
            foreach (var version in await queries.ToListAsync(versions.Query().Where(v => v.TenantId == actor.TenantId && v.DocumentId == id), token))
            { version.IsDeleted = true; versions.Update(version); }
            foreach (var grant in await queries.ToListAsync(grants.Query().Where(g => g.TenantId == actor.TenantId && g.DocumentId == id), token)) grants.Remove(grant);
            foreach (var file in await queries.ToListAsync(files.Query().Where(f => f.TenantId == actor.TenantId && f.BusinessType == AiKnowledgeContract.BusinessType && f.BusinessId == id), token))
            { file.FileStatus = FileStatus.PendingDelete; file.NextRetryAt = null; file.LastError = null; files.Update(file); }
            var affectedRunIds = references.Query().Where(r => r.TenantId == actor.TenantId && r.DocumentId == id).Select(r => r.RunId);
            var affectedRuns = await queries.ToListAsync(runs.Query().Where(r => r.TenantId == actor.TenantId && affectedRunIds.Contains(r.Id)), token);
            var responseIds = affectedRuns.Where(r => r.ResponseMessageId.HasValue).Select(r => r.ResponseMessageId!.Value).ToArray();
            var conversationIds = affectedRuns.Select(r => r.ConversationId).Distinct().ToArray();
            var runIds = affectedRuns.Select(r => r.Id).ToHashSet();
            foreach (var message in await queries.ToListAsync(messages.Query().Where(m => m.TenantId == actor.TenantId &&
                (responseIds.Contains(m.Id) || (conversationIds.Contains(m.ConversationId) && m.Role == AiMessageRole.Tool &&
                    m.Content.Contains(AiKnowledgeContract.ToolCode)))), token))
            {
                if (!responseIds.Contains(message.Id))
                {
                    try
                    {
                        var envelope = JsonSerializer.Deserialize<AiStructuredResultEnvelope>(message.Content, AiStructuredResults.JsonOptions);
                        if (envelope?.Result.ToolCode != AiKnowledgeContract.ToolCode || !runIds.Contains(envelope.Result.RunId)) continue;
                    }
                    catch (JsonException) { continue; }
                }
                message.Content = "[expired]"; message.ContentDigest = "EXPIRED"; message.TokenCount = null; messages.Update(message);
            }
            foreach (var invocation in await queries.ToListAsync(invocations.Query().Where(i => i.TenantId == actor.TenantId && affectedRunIds.Contains(i.RunId)), token))
            { invocation.CitationJson = null; invocation.OutputDigest = null; invocations.Update(invocation); }
            await unit.SaveChangesAsync(token);
        }, ct);
    }

    public async Task<AiKnowledgeSearchResult> SearchAsync(AiKnowledgeSearchRequest request, CancellationToken ct = default)
    {
        var actor = await access.AuthorizeAsync(AiCenterConstants.KnowledgeQueryPermission, ct);
        var keyword = Required(request.Keyword, 100);
        if (request.Limit is < 1 or > AiKnowledgeContract.MaxResults || request.DocumentId == Guid.Empty) throw Invalid("Invalid knowledge search parameters.");
        var now = DateTimeOffset.UtcNow;
        var visible = access.VisibleVersions(actor, now);
        var documentQuery = documents.Query();
        var query = from chunk in chunks.Query()
                    join version in visible on chunk.VersionId equals version.Id
                    join document in documentQuery on chunk.DocumentId equals document.Id
                    where !chunk.IsDeleted && chunk.TenantId == actor.TenantId && chunk.DocumentId == version.DocumentId &&
                        (!request.DocumentId.HasValue || chunk.DocumentId == request.DocumentId.Value) && chunk.Content.Contains(keyword)
                    orderby document.Title, document.Id, chunk.Sequence
                    select new AiKnowledgeHit(new(chunk.DocumentId, version.Id, chunk.Id, chunk.ContentHash), document.Title,
                        version.VersionNumber, chunk.StartLine, chunk.EndLine, chunk.Content, version.ValidFrom, version.ValidUntil);
        var hits = await queries.ToListAsync(query.Take(request.Limit + 1), ct);
        return new(hits.Take(request.Limit).ToArray(), now, hits.Count > request.Limit);
    }

    public async Task<AiKnowledgeHit> ReadChunkAsync(AiKnowledgeReference reference, CancellationToken ct = default)
    {
        var actor = await access.AuthorizeAsync(AiCenterConstants.KnowledgeQueryPermission, ct);
        var visible = access.VisibleVersions(actor, DateTimeOffset.UtcNow);
        var documentQuery = documents.Query();
        var hit = await queries.FirstOrDefaultAsync(from chunk in chunks.Query()
            join version in visible on chunk.VersionId equals version.Id
            join document in documentQuery on chunk.DocumentId equals document.Id
            where !chunk.IsDeleted && chunk.TenantId == actor.TenantId && chunk.Id == reference.ChunkId &&
                chunk.DocumentId == reference.DocumentId && version.DocumentId == reference.DocumentId &&
                version.Id == reference.VersionId && chunk.ContentHash == reference.ContentHash && chunk.Content != ""
            select new AiKnowledgeHit(reference, document.Title, version.VersionNumber, chunk.StartLine, chunk.EndLine,
                chunk.Content, version.ValidFrom, version.ValidUntil), ct) ?? throw Unavailable();
        if (AiStructuredResults.Digest(hit.Content) != reference.ContentHash) throw Unavailable();
        return hit;
    }

    public async Task<AiKnowledgeSearchResult> PreviewAsync(Guid id, Guid versionId, int startSequence = 1, CancellationToken ct = default)
    {
        var actor = await access.AuthorizeAsync(AiCenterConstants.KnowledgeQueryPermission, ct);
        if (startSequence < 1 || startSequence > AiKnowledgeContract.MaxChunks) throw Invalid("Invalid preview sequence.");
        var now = DateTimeOffset.UtcNow;
        var accessible = access.ReviewableVersions(actor, now);
        var documentQuery = documents.Query();
        var query = from chunk in chunks.Query()
                    join version in accessible on chunk.VersionId equals version.Id
                    join document in documentQuery on chunk.DocumentId equals document.Id
                    where !chunk.IsDeleted && chunk.TenantId == actor.TenantId && chunk.DocumentId == id &&
                        version.DocumentId == id && version.Id == versionId && chunk.Sequence >= startSequence && chunk.Content != ""
                    orderby chunk.Sequence
                    select new AiKnowledgeHit(new(chunk.DocumentId, version.Id, chunk.Id, chunk.ContentHash), document.Title,
                        version.VersionNumber, chunk.StartLine, chunk.EndLine, chunk.Content, version.ValidFrom, version.ValidUntil);
        var hits = await queries.ToListAsync(query.Take(AiKnowledgeContract.MaxResults + 1), ct);
        if (hits.Count == 0) throw Unavailable();
        return new(hits.Take(AiKnowledgeContract.MaxResults).ToArray(), now, hits.Count > AiKnowledgeContract.MaxResults,
            "审核预览；未发布资料不会用于模型检索。下一页从当前序号加 5 开始。");
    }

    private async Task<AiKnowledgeDocument> GetDocumentAsync(Guid id, Guid tenantId, CancellationToken ct) =>
        await queries.FirstOrDefaultAsync(documents.Query().Where(d => !d.IsDeleted && d.TenantId == tenantId && d.Id == id), ct) ?? throw Unavailable();

    private async Task CheckTokenAsync(AiKnowledgeDocument document, byte[] supplied, CancellationToken ct, bool forWrite = false)
    {
        if (forWrite && mutationFence is not null) await mutationFence.HoldDocumentAsync(document.Id, ct);
        var stored = await queries.FirstOrDefaultAsync(documents.Query().Where(d => !d.IsDeleted && d.TenantId == document.TenantId && d.Id == document.Id)
            .Select(d => new { d.RowVersion, d.AccessVersion, d.LastVersionNumber }), ct) ?? throw Unavailable();
        var token = stored.RowVersion.Length > 0 ? stored.RowVersion : BitConverter.GetBytes(stored.AccessVersion + stored.LastVersionNumber);
        if (supplied is null || !supplied.SequenceEqual(token) || !Token(document).SequenceEqual(token))
            throw new BusinessException(ErrorCode.Conflict, "Knowledge document changed; reload before retrying.");
    }

    private async Task<Guid[]> ValidateRolesAsync(Guid tenantId, Guid[] requested, CancellationToken ct)
    {
        if (requested is null || requested.Length > 100 || requested.Any(r => r == Guid.Empty)) throw Invalid("Invalid knowledge roles.");
        var ids = requested.Distinct().ToArray();
        if (await queries.LongCountAsync(roles.Query().Where(r => !r.IsDeleted && r.IsEnabled && r.TenantId == tenantId && ids.Contains(r.Id)), ct) != ids.Length)
            throw Invalid("Knowledge roles must belong to the current tenant and be active.");
        return ids;
    }

    private async Task<AiKnowledgeDocumentResponse> ToResponseAsync(AiKnowledgeDocument d, CancellationToken ct) => new(d.Id, d.Title, d.Owner, d.License,
        d.CurrentVersionId, d.AccessVersion, Token(d), await queries.ToListAsync(grants.Query().Where(g => g.TenantId == d.TenantId && g.DocumentId == d.Id).Select(g => g.RoleId), ct),
        await queries.ToListAsync(versions.Query().Where(v => !v.IsDeleted && v.TenantId == d.TenantId && v.DocumentId == d.Id)
            .OrderByDescending(v => v.VersionNumber).Select(v => new AiKnowledgeVersionResponse(v.Id, v.VersionNumber, v.ParseStatus.ToString(),
                v.ErrorCode, v.ValidFrom, v.ValidUntil, v.PublishedAt)), ct));

    private static byte[] Token(AiKnowledgeDocument d) => d.RowVersion.Length > 0 ? d.RowVersion : BitConverter.GetBytes(d.AccessVersion + d.LastVersionNumber);
    private static string Required(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max
        ? value.Trim() : throw Invalid("A bounded knowledge field is required.");
    private static BusinessException Invalid(string message) => new(ErrorCode.ValidationFailed, message);
    private static BusinessException Unavailable() => new(ErrorCode.NotFound, "Knowledge source is unavailable.");
}
