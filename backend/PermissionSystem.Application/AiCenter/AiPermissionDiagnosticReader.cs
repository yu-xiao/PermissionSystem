using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Application.AiCenter;

public sealed record AiPermissionDiagnosticResult(Guid RunId, string InvocationId, PermissionDiagnosticResponse Data);

public sealed class AiPermissionDiagnosticEnvelope
{
    public const string ResultType = "permission-diagnostic";
    public string Type { get; init; } = ResultType;
    public int Version { get; init; } = 1;
    public Guid RunId { get; init; }
    public string InvocationId { get; init; } = string.Empty;
    public PermissionDiagnosticResponse? Data { get; init; }
}

public interface IAiPermissionDiagnosticReader
{
    Task<IReadOnlyList<AiPermissionDiagnosticResult>> ReadAsync(
        Guid conversationId, Guid? runId = null, CancellationToken cancellationToken = default);
}

public sealed class AiPermissionDiagnosticReader(
    IRepository<AiMessage> messages, IRepository<AiRun> runs,
    IRepository<AiConversation> conversations, IRepository<AiToolInvocation> invocations,
    ICurrentUserService currentUser, ITenantContext tenant, IAsyncQueryExecutor queries,
    IPermissionDiagnosticService diagnostics) : IAiPermissionDiagnosticReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<AiPermissionDiagnosticResult>> ReadAsync(
        Guid conversationId, Guid? runId = null, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || !currentUser.TenantId.HasValue ||
            currentUser.TenantId != tenant.TenantId || !currentUser.HasPermission(AiCenterConstants.ConversationViewPermission)) return [];
        var tenantId = currentUser.TenantId.Value;
        if (!await queries.AnyAsync(conversations.Query().Where(conversation => conversation.Id == conversationId &&
                conversation.TenantId == tenantId && conversation.UserId == currentUser.UserId &&
                conversation.Status != AiConversationStatus.Deleted), cancellationToken)) return [];
        var ownedRuns = runs.Query().Where(run => run.ConversationId == conversationId && run.TenantId == tenantId &&
            run.ActorUserId == currentUser.UserId && (!runId.HasValue || run.Id == runId.Value));
        var validInvocations = await queries.ToListAsync(
            from invocation in invocations.Query()
            join run in ownedRuns on invocation.RunId equals run.Id
            where invocation.TenantId == tenantId && invocation.ToolCode == PermissionDiagnosticAiToolHandler.ToolCode &&
                invocation.ToolVersion == "1.0" && invocation.Status == AiInvocationStatus.Completed
            select invocation, cancellationToken);
        if (validInvocations.Count == 0) return [];
        var lookup = validInvocations.ToDictionary(item => (item.RunId, item.InvocationId));
        var toolMessages = await queries.ToListAsync(messages.Query().Where(message => message.TenantId == tenantId &&
            message.ConversationId == conversationId && message.Role == AiMessageRole.Tool && !message.ModelGenerated)
            .OrderBy(message => message.Sequence), cancellationToken);
        var results = new List<AiPermissionDiagnosticResult>();
        var seen = new HashSet<(Guid, string)>();
        foreach (var message in toolMessages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (message.Content.Length > PermissionDiagnosticResponse.MaxSerializedLength + 1024) continue;
            AiPermissionDiagnosticEnvelope? envelope;
            try { envelope = JsonSerializer.Deserialize<AiPermissionDiagnosticEnvelope>(message.Content, JsonOptions); }
            catch (JsonException) { continue; }
            if (envelope?.Type != AiPermissionDiagnosticEnvelope.ResultType || envelope.Version != 1 || envelope.Data is null ||
                !lookup.TryGetValue((envelope.RunId, envelope.InvocationId), out var invocation)) continue;
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope.Data, JsonOptions))));
            if (digest != invocation.OutputDigest || !seen.Add((envelope.RunId, envelope.InvocationId)) ||
                !await diagnostics.CanReadAsync(envelope.Data, cancellationToken)) continue;
            results.Add(new AiPermissionDiagnosticResult(envelope.RunId, envelope.InvocationId, envelope.Data));
        }
        return results;
    }
}
