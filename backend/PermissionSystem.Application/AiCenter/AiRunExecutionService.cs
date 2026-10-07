using PermissionSystem.Application.Authentication;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiCenter;

public sealed partial class AiConversationService
{
    private readonly IAiRunExecutionStore? _executionStore;
    private readonly AiRunIdentityValidator? _identityValidator;
    private readonly AiRunExecutionFence? _executionFence;
    private readonly IUserCredentialValidator? _identities;

    public async Task<AiRunResponse> GetSubmissionAsync(Guid conversationId, string submissionKey, CancellationToken cancellationToken = default)
    {
        ValidateSubmissionKey(submissionKey);
        var identity = EnsureAccess(AiCenterConstants.ConversationViewPermission);
        _ = await GetOwnedConversationAsync(conversationId, identity.UserId, cancellationToken);
        var hash = ComputeDigest(submissionKey.Trim());
        var run = await _queryExecutor.FirstOrDefaultAsync(_runRepository.Query().Where(r => r.ConversationId == conversationId &&
            r.ActorUserId == identity.UserId && r.SubmissionHash == hash), cancellationToken);
        if (run is null) throw new BusinessException(ErrorCode.NotFound, "The AI submission was not found.");
        return await ToRunResponseAsync(run, cancellationToken);
    }

    public Task<AiRunResponse> SubmitAsync(Guid conversationId, SendAiMessageRequest request, string submissionKey,
        CancellationToken cancellationToken = default)
    {
        ValidateSubmissionKey(submissionKey);
        return SendMessageCoreAsync(conversationId, request, null, cancellationToken, submissionKey, true);
    }

    public async Task<AiRunResponse> SubmitRetryAsync(Guid runId, string submissionKey, CancellationToken cancellationToken = default)
    {
        ValidateSubmissionKey(submissionKey);
        var identity = EnsureAccess(AiCenterConstants.ChatUsePermission);
        var original = await GetOwnedRunAsync(runId, identity.UserId, cancellationToken);
        if (original.Status is not (AiRunStatus.Failed or AiRunStatus.Cancelled))
            throw new BusinessException(ErrorCode.Conflict, "Only failed or cancelled AI runs can be retried.");
        var message = await _messageRepository.GetByIdAsync(original.RequestMessageId, cancellationToken);
        if (message is null || message.Content == "[expired]" || message.CreatedAt < DateTimeOffset.UtcNow.AddDays(-_configuration.ConversationRetentionDays))
            throw new BusinessException(ErrorCode.Conflict, "The original AI request is unavailable.");
        var context = await ReadRequestContextAsync(original, cancellationToken);
        return await SendMessageCoreAsync(original.ConversationId, new() { Content = message.Content,
            ContextRef = context.ContextRef, UtcOffsetMinutes = context.UtcOffsetMinutes }, runId, cancellationToken, submissionKey, true);
    }

    public async Task<AiRunResponse> WaitAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var identity = EnsureAccess(AiCenterConstants.ChatUsePermission);
        if (_executionStore is null) return await GetRunAsync(runId, cancellationToken);
        while (true)
        {
            var run = await _executionStore.FindAsync(runId, cancellationToken);
            if (run is null || run.ActorUserId != identity.UserId) throw new BusinessException(ErrorCode.NotFound, "AI run was not found.");
            if (run.Status is not (AiRunStatus.Pending or AiRunStatus.Running)) return await ToRunResponseAsync(run, cancellationToken);
            await Task.Delay(500, cancellationToken);
        }
    }

    public async Task ExecuteAsync(Guid runId, Guid leaseId, CancellationToken cancellationToken = default)
    {
        if (_executionStore is null || _executionFence is null) throw new InvalidOperationException("Persistent AI execution is not configured.");
        var run = await _runRepository.GetByIdAsync(runId, cancellationToken) ?? throw new AiRunLeaseLostException();
        if (run.ExecutionMode != "Background" || run.Status != AiRunStatus.Running || run.ExecutionLeaseId != leaseId)
            throw new AiRunLeaseLostException();
        _executionFence.Begin(runId, leaseId);
        await _identityValidator!.ValidateAsync(run, cancellationToken);
        EnsureAccess(AiCenterConstants.ChatUsePermission);
        var build = _buildIdentity?.Identity ?? AiScenarioSnapshots.Digest(typeof(AiConversationService).Assembly.ManifestModule.ModuleVersionId.ToString());
        if (run.BuildIdentity != build) throw new BusinessException(ErrorCode.Conflict, "The admitted AI build is incompatible.");
        var conversation = await GetOwnedConversationAsync(run.ConversationId, run.ActorUserId, cancellationToken);
        if (conversation.Status != AiConversationStatus.Active || conversation.ScenarioVersionId != run.ScenarioVersionId)
            throw new BusinessException(ErrorCode.Conflict, "The admitted AI conversation is unavailable.");
        var message = await _messageRepository.GetByIdAsync(run.RequestMessageId, cancellationToken);
        if (message is null || message.ConversationId != run.ConversationId || message.ContentDigest != ComputeDigest(message.Content) ||
            message.Content == "[expired]" || message.CreatedAt < DateTimeOffset.UtcNow.AddDays(-_configuration.ConversationRetentionDays))
            throw new BusinessException(ErrorCode.Conflict, "The admitted AI request is unavailable.");
        var context = await ReadRequestContextAsync(run, cancellationToken);
        var expected = ComputeDigest(System.Text.Json.JsonSerializer.Serialize(new { Content = message.Content,
            context.ContextRef, context.UtcOffsetMinutes, retryOfRunId = run.RetryOfRunId }, JsonOptions));
        if (expected != run.RequestHash) throw new BusinessException(ErrorCode.Conflict, "The admitted AI request is invalid.");
        await ValidateScenarioRunAsync(run, cancellationToken);
        var snapshot = run.ScenarioVersionId.HasValue ? await _scenarioRuntime!.ValidateAsync(run.ScenarioVersionId.Value, cancellationToken) : null;
        var routes = await ResolveRouteCandidatesAsync(run.AgentCode, run.ConversationId, cancellationToken);
        await ExecuteRunAsync(run, conversation, routes, run.ActorUserId, context.ContextRef, context.UtcOffsetMinutes, snapshot, cancellationToken);
    }

    private void ValidateSubmissionKey(string key)
    {
        if (_executionStore is null) throw new BusinessException(ErrorCode.Conflict, "Persistent AI execution is unavailable.");
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
            throw new BusinessException(ErrorCode.ValidationFailed, "A submission key of at most 128 characters is required.");
    }
}
