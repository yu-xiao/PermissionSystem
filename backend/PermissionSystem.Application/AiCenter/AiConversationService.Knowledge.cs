using PermissionSystem.Application.AiKnowledge;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiCenter;

public sealed partial class AiConversationService
{
    private async Task<bool> CanReadKnowledgeRunAsync(Guid runId, CancellationToken ct)
    {
        if (_knowledgeRuns is not null) return await _knowledgeRuns.CanReadAsync(runId, ct);
        return !await _queryExecutor.AnyAsync(_toolInvocationRepository.Query()
            .Where(i => i.RunId == runId && i.ToolCode == AiKnowledgeContract.ToolCode), ct);
    }

    private async Task EnsureKnowledgeEvidenceAsync(Guid runId, CancellationToken ct, bool forCommit = false)
    {
        if (forCommit && _knowledgeRuns is not null) await _knowledgeRuns.EnsureCommitAsync(runId, ct);
        if (!await CanReadKnowledgeRunAsync(runId, ct))
            throw new BusinessException(ErrorCode.Forbidden, "Knowledge evidence is no longer available.");
    }

    private async Task<IReadOnlyList<AiMessage>> ProtectKnowledgeMessagesAsync(
        IReadOnlyList<AiMessage> messages, IReadOnlyList<AiRun> responseRuns, CancellationToken ct)
    {
        var unavailableIds = new HashSet<Guid>();
        foreach (var run in responseRuns)
            if (run.ResponseMessageId.HasValue && !await CanReadKnowledgeRunAsync(run.Id, ct)) unavailableIds.Add(run.ResponseMessageId.Value);
        return messages.Select(m => unavailableIds.Contains(m.Id) ? UnavailableKnowledgeMessage(m) : m).ToArray();
    }

    private static AiMessage UnavailableKnowledgeMessage(AiMessage source) => new()
    {
        Id = source.Id, TenantId = source.TenantId, ConversationId = source.ConversationId,
        Role = source.Role, Sequence = source.Sequence, CreatedAt = source.CreatedAt,
        Content = AiKnowledgeContract.Unavailable, ModelGenerated = false
    };
}
