using System.Text;
using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiKnowledge;

public interface IAiKnowledgeRunGuard
{
    Task BindAsync(AiRun run, string invocationId, AiToolExecutionResult result, CancellationToken ct);
    Task<bool> CanReadAsync(Guid runId, CancellationToken ct);
    Task EnsureValidAsync(Guid runId, CancellationToken ct);
    Task EnsureCommitAsync(Guid runId, CancellationToken ct);
}

public sealed class AiKnowledgeRunGuard(
    IAiKnowledgeService service, AiKnowledgeAccessPolicy access, IRepository<AiKnowledgeRunReference> references,
    IRepository<AiRun> runs, IRepository<AiToolInvocation> invocations, IAsyncQueryExecutor queries,
    ICurrentUserService current, IAiKnowledgeCommitFence? commitFence = null,
    IRepository<AiMessage>? messages = null) : IAiKnowledgeRunGuard
{
    public async Task BindAsync(AiRun run, string invocationId, AiToolExecutionResult result, CancellationToken ct)
    {
        var items = result.StructuredResult?.KnowledgeReferences;
        if (items is null || result.KnowledgeHits is null || items.Count != result.KnowledgeHits.Count ||
            items.Count != result.RowCount || items.Count > AiKnowledgeContract.MaxResults || items.Distinct().Count() != items.Count)
            throw Invalid();
        var actor = await access.AuthorizeAsync(AiCenterConstants.KnowledgeQueryPermission, ct);
        if (run.TenantId != actor.TenantId || run.ActorUserId != actor.UserId ||
            !await queries.AnyAsync(runs.Query().Where(r => !r.IsDeleted && r.Id == run.Id && r.TenantId == actor.TenantId &&
                r.ActorUserId == actor.UserId && r.Status == AiRunStatus.Running), ct)) throw Invalid();
        foreach (var item in items)
        {
            var source = await service.ReadChunkAsync(item, ct);
            if (!result.KnowledgeHits.Any(h => h.Reference == item && h.Content == source.Content)) throw Invalid();
        }
        if (await queries.AnyAsync(references.Query().Where(r => r.TenantId == actor.TenantId && r.RunId == run.Id && r.InvocationId == invocationId), ct)) throw Invalid();
        foreach (var item in items) await references.AddAsync(new AiKnowledgeRunReference
        {
            Id = Guid.NewGuid(), TenantId = actor.TenantId, RunId = run.Id, InvocationId = invocationId,
            DocumentId = item.DocumentId, VersionId = item.VersionId, ChunkId = item.ChunkId, ContentHash = item.ContentHash
        }, ct);
    }

    public async Task<bool> CanReadAsync(Guid runId, CancellationToken ct)
    {
        var tenantId = current.TenantId;
        var calls = await queries.ToListAsync(invocations.Query().Where(i => !i.IsDeleted && i.TenantId == tenantId &&
            i.RunId == runId && i.ToolCode == AiKnowledgeContract.ToolCode)
            .Select(i => new { i.InvocationId, i.Status, i.RowCount, i.OutputDigest, i.InputDigest, i.ToolVersion }), ct);
        var boundCalls = await queries.ToListAsync(references.Query().Where(r => !r.IsDeleted && r.TenantId == tenantId && r.RunId == runId)
            .Select(r => r.InvocationId).Distinct(), ct);
        if (boundCalls.Except(calls.Select(c => c.InvocationId), StringComparer.Ordinal).Any()) return false;
        if (calls.Count == 0) return true;
        try
        {
            var actor = await access.AuthorizeAsync(AiCenterConstants.KnowledgeQueryPermission, ct);
            var run = await queries.FirstOrDefaultAsync(runs.Query().Where(r => !r.IsDeleted && r.Id == runId && r.TenantId == actor.TenantId && r.ActorUserId == actor.UserId)
                .Select(r => new { r.ConversationId }), ct);
            if (run is null || messages is null) return false;
            foreach (var call in calls)
            {
                if (call.Status != AiInvocationStatus.Completed || call.RowCount is null or < 0 or > AiKnowledgeContract.MaxResults ||
                    string.IsNullOrWhiteSpace(call.OutputDigest)) return false;
                var items = await queries.ToListAsync(references.Query().Where(r => !r.IsDeleted && r.TenantId == actor.TenantId && r.RunId == runId &&
                    r.InvocationId == call.InvocationId).Select(r => new AiKnowledgeReference(r.DocumentId, r.VersionId, r.ChunkId, r.ContentHash)), ct);
                if (items.Count != call.RowCount || items.Distinct().Count() != items.Count) return false;
                var envelopes = await queries.ToListAsync(messages.Query().Where(m => !m.IsDeleted && m.TenantId == actor.TenantId &&
                    m.ConversationId == run.ConversationId && m.Role == AiMessageRole.Tool && !m.ModelGenerated &&
                    m.ContentDigest == call.OutputDigest && m.Content.Length <= AiStructuredResults.MaxEnvelopeBytes)
                    .Select(m => new { m.Content, m.ContentDigest }).Take(2), ct);
                if (envelopes.Count != 1) return false;
                var stored = envelopes[0];
                if (Encoding.UTF8.GetByteCount(stored.Content) > AiStructuredResults.MaxEnvelopeBytes ||
                    AiStructuredResults.Digest(stored.Content) != stored.ContentDigest) return false;
                var envelope = JsonSerializer.Deserialize<AiStructuredResultEnvelope>(stored.Content, AiStructuredResults.JsonOptions);
                var result = envelope?.Result;
                if (envelope?.Type != "structured-result" || envelope.Version != 1 || result is null || result.Version != 1 ||
                    result.RunId != runId || result.InvocationId != call.InvocationId || result.Type != "knowledge-citations" ||
                    result.ToolCode != AiKnowledgeContract.ToolCode || result.ToolVersion != "1.0" || call.ToolVersion != result.ToolVersion ||
                    result.KnowledgeHits is not null || result.KnowledgeReferences is not { } refs || refs.Count != items.Count ||
                    refs.Distinct().Count() != refs.Count || !refs.ToHashSet().SetEquals(items) || result.Citation is null ||
                    result.Citation.ToolCode != result.ToolCode || result.Citation.ToolVersion != result.ToolVersion ||
                    result.Citation.RowCount != items.Count || result.Citation.QueryParametersDigest != call.InputDigest) return false;
                foreach (var item in items) await service.ReadChunkAsync(item, ct);
            }
            return true;
        }
        catch (BusinessException e) when (e.ErrorCode is ErrorCode.Forbidden or ErrorCode.Unauthorized or ErrorCode.NotFound or ErrorCode.ValidationFailed) { return false; }
        catch (JsonException) { return false; }
    }

    public async Task EnsureValidAsync(Guid runId, CancellationToken ct)
    {
        if (!await CanReadAsync(runId, ct)) throw Invalid();
    }

    public async Task EnsureCommitAsync(Guid runId, CancellationToken ct)
    {
        if (commitFence is not null && await queries.AnyAsync(invocations.Query().Where(i => i.RunId == runId &&
            i.TenantId == current.TenantId && i.ToolCode == AiKnowledgeContract.ToolCode), ct)) await commitFence.HoldAsync(runId, ct);
        await EnsureValidAsync(runId, ct);
    }

    private static BusinessException Invalid() => new(ErrorCode.Forbidden, "Knowledge evidence is unavailable; query again.");
}
