using System.Text;
using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiCenter;

public interface IAiStructuredResultReader
{
    Task<AiStructuredResultPage> ReadAsync(Guid conversationId, Guid? runId = null, CancellationToken cancellationToken = default);
}

public sealed class AiStructuredResultReader(
    IRepository<AiMessage> messages, IRepository<AiRun> runs, IRepository<AiConversation> conversations,
    IRepository<AiToolInvocation> invocations, IRepository<User> users, IRepository<Department> departments,
    ICurrentUserService currentUser, ITenantContext tenant, IAsyncQueryExecutor queries,
    IPermissionDiagnosticService diagnostics, IAiQueryAccessGuard access, IDataPermissionFilter filter,
    IAiCenterConfiguration configuration, IRepository<Menu>? menus = null) : IAiStructuredResultReader
{
    private const int MaxMessages = 50;

    public async Task<AiStructuredResultPage> ReadAsync(Guid conversationId, Guid? runId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || currentUser.TenantId is not Guid tenantId ||
            tenantId != tenant.TenantId || !configuration.Enabled || !configuration.AllowedTenantIds.Contains(tenantId) ||
            !currentUser.HasPermission(AiCenterConstants.ConversationViewPermission)) return new([]);
        if (!await queries.AnyAsync(conversations.Query().Where(item => !item.IsDeleted && item.Id == conversationId &&
            item.TenantId == tenantId && item.UserId == userId && item.Status != AiConversationStatus.Deleted), cancellationToken)) return new([]);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-configuration.ConversationRetentionDays);
        var ownedRuns = runs.Query().Where(item => !item.IsDeleted && item.TenantId == tenantId && item.CreatedAt >= cutoff && item.ConversationId == conversationId &&
            item.ActorUserId == userId && (!runId.HasValue || item.Id == runId));
        var invocationQuery =
            from invocation in invocations.Query()
            join run in ownedRuns on invocation.RunId equals run.Id
            where !invocation.IsDeleted && invocation.TenantId == tenantId && invocation.Status == AiInvocationStatus.Completed &&
                (invocation.ToolCode == "permission.diagnose" || invocation.ToolCode == "permission.users.search" ||
                 invocation.ToolCode == "permission.login_logs.summary" || invocation.ToolCode == "permission.operation_logs.summary")
            select invocation;
        var validInvocations = await queries.ToListAsync(invocationQuery.OrderByDescending(item => item.CreatedAt).Take(MaxMessages * 10 + 1), cancellationToken);
        if (validInvocations.Count == 0) return new([]);
        var lookup = validInvocations.GroupBy(item => (item.RunId, item.InvocationId))
            .Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single());
        var runText = runId?.ToString();
        var sourceQuery = messages.Query().Where(item => !item.IsDeleted && item.TenantId == tenantId &&
            item.ConversationId == conversationId && item.Role == AiMessageRole.Tool && !item.ModelGenerated &&
            (runText == null || item.Content.Contains(runText)) &&
            (item.Content.Contains("\"type\":\"structured-result\"") || item.Content.Contains("\"type\":\"permission-diagnostic\"") || item.Content == "[expired]"));
        var sourceMessages = await queries.ToListAsync(sourceQuery.Where(item => item.CreatedAt >= cutoff && item.Content.Length <= AiStructuredResults.MaxEnvelopeBytes)
            .OrderByDescending(item => item.Sequence).Take(MaxMessages + 1), cancellationToken);
        var unavailable = await queries.AnyAsync(sourceQuery.Where(item => item.CreatedAt < cutoff || item.Content.Length > AiStructuredResults.MaxEnvelopeBytes), cancellationToken);
        var limited = sourceMessages.Count > MaxMessages || validInvocations.Count > MaxMessages * 10;
        var results = new List<AiStructuredResult>();
        var seen = new HashSet<(Guid, string)>();
        var size = 0;
        foreach (var message in sourceMessages.Take(MaxMessages))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (message.Content == "[expired]" || Encoding.UTF8.GetByteCount(message.Content) > AiStructuredResults.MaxEnvelopeBytes)
            { unavailable = true; continue; }
            AiStructuredResult? result = null;
            try
            {
                using var document = JsonDocument.Parse(message.Content);
                if (!document.RootElement.TryGetProperty("type", out var type)) continue;
                if (type.GetString() == "structured-result")
                {
                    var envelope = JsonSerializer.Deserialize<AiStructuredResultEnvelope>(message.Content, AiStructuredResults.JsonOptions);
                    if (envelope?.Version != 1 || envelope.Result is null) { unavailable = true; continue; }
                    result = envelope.Result;
                    if (!lookup.TryGetValue((result.RunId, result.InvocationId), out var invocation) ||
                        message.ContentDigest != AiStructuredResults.Digest(message.Content) ||
                        invocation.OutputDigest != message.ContentDigest || result.ToolCode != invocation.ToolCode ||
                        result.ToolVersion != invocation.ToolVersion || result.Citation is null || result.Citation.ToolCode != invocation.ToolCode ||
                        result.Citation.ToolVersion != invocation.ToolVersion || result.Citation.QueryParametersDigest != invocation.InputDigest)
                    { unavailable = true; continue; }
                }
                else if (type.GetString() == AiPermissionDiagnosticEnvelope.ResultType)
                {
                    var envelope = JsonSerializer.Deserialize<AiPermissionDiagnosticEnvelope>(message.Content, AiStructuredResults.JsonOptions);
                    if (envelope?.Version != 1 || envelope.Data is null ||
                        !lookup.TryGetValue((envelope.RunId, envelope.InvocationId), out var invocation) ||
                        invocation.ToolCode != "permission.diagnose" || invocation.ToolVersion != "1.0" ||
                        invocation.OutputDigest != AiStructuredResults.Digest(JsonSerializer.Serialize(envelope.Data, AiStructuredResults.JsonOptions)))
                    { unavailable = true; continue; }
                    var target = envelope.Data.Target;
                    result = new AiStructuredResult
                    {
                        RunId = envelope.RunId, InvocationId = envelope.InvocationId, Type = "permission-diagnostic",
                        ToolCode = invocation.ToolCode, ToolVersion = invocation.ToolVersion, QueriedAt = envelope.Data.EvaluatedAt,
                        EvaluationBasis = envelope.Data.EvaluationBasis, Diagnostic = envelope.Data, IsTruncated = envelope.Data.IsTruncated,
                        Context = new() { Parameters = AiStructuredResults.Parameters(new
                        {
                            kind = target.Kind, targetUserId = target.UserId == userId ? (Guid?)null : target.UserId,
                            target.MenuId, target.PermissionCode
                        }) },
                        Citation = new() { ToolCode = invocation.ToolCode, ToolVersion = invocation.ToolVersion,
                            QueriedAt = envelope.Data.EvaluatedAt, RowCount = 1, QueryParametersDigest = invocation.InputDigest }
                    };
                }
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            { unavailable = true; continue; }
            if (result is null) continue;
            if (!await CanReadAsync(result, cancellationToken)) { unavailable = true; continue; }
            if (!seen.Add((result.RunId, result.InvocationId))) continue;
            var resultSize = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(result, AiStructuredResults.JsonOptions));
            if (size + resultSize > AiStructuredResults.MaxResponseBytes) { limited = true; continue; }
            size += resultSize;
            results.Add(result);
        }
        results.Reverse();
        return new(results, unavailable, limited);
    }

    private async Task<bool> CanReadAsync(AiStructuredResult result, CancellationToken cancellationToken)
    {
        if (result.Version != 1 || result.Context is null || result.Context.Version != 1 || result.Context.Parameters is null ||
            !AiStructuredResults.IsSupported(result.ToolCode) || result.ToolVersion != "1.0" ||
            result.Type != AiStructuredResults.ResultType(result.ToolCode) || result.RunId == Guid.Empty ||
            string.IsNullOrWhiteSpace(result.InvocationId) || result.InvocationId.Length > 100 ||
            result.Context.Parameters.Keys.Except(AiStructuredResults.AllowedParameters(result.ToolCode), StringComparer.Ordinal).Any() ||
            Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(result.Context, AiStructuredResults.JsonOptions)) > AiStructuredResults.MaxContextBytes) return false;
        try
        {
            var actor = await access.AuthorizeAsync(result.ToolCode, cancellationToken);
            if (!PermissionEvaluation.HasPermission(true, PermissionEvaluation.IsSuperAdmin(actor.Roles), actor.PermissionCodes,
                AiCenterConstants.ConversationViewPermission)) return false;
            if (result.Diagnostic is not null)
            {
                if (result.Diagnostic.Target.Kind == PermissionDiagnosticKind.Menu && (menus is null ||
                    !await queries.AnyAsync(menus.Query().Where(item => !item.IsDeleted && item.TenantId == actor.TenantId &&
                        item.Id == result.Diagnostic.Target.MenuId), cancellationToken))) return false;
                return result.Type == "permission-diagnostic" && result.Table is null && result.Statistics is null &&
                    await diagnostics.CanReadAsync(result.Diagnostic, cancellationToken);
            }
            if (result.Type == "statistics-summary")
                return result.Table is null && result.Statistics is { TotalCount: >= 0 } statistics && statistics.Groups is not null &&
                    statistics.Groups.Count <= 2 && statistics.Groups.All(group => group.Items is not null &&
                        group.TotalGroupCount >= group.Items.Count && group.Items.All(item => item.Count >= 0));
            if (result.Type != "table" || result.Table is null || result.Table.Items is null || result.Statistics is not null ||
                result.Table.TotalCount < result.Table.Items.Count) return false;
            var scope = await access.GetUserScopeAsync(actor, cancellationToken);
            if (result.Context.DataScopeFingerprint != AiStructuredResults.ScopeFingerprint(scope)) return false;
            if (result.Context.Parameters.TryGetValue("departmentScope", out var departmentScope) && departmentScope.GetString() == "CurrentDepartment" &&
                (actor.DepartmentId is not Guid departmentId || !await queries.AnyAsync(departments.Query().Where(item =>
                    item.TenantId == actor.TenantId && item.Id == departmentId && !item.IsDeleted && item.IsEnabled), cancellationToken))) return false;
            var ids = result.Table.Items.Select(item => item.Id).Distinct().ToArray();
            if (ids.Length != result.Table.Items.Count) return false;
            return await queries.LongCountAsync(users.Query().Where(item => !item.IsDeleted && item.TenantId == actor.TenantId && ids.Contains(item.Id))
                .ApplyDataPermission(filter, scope, item => (Guid?)item.Id, item => item.DepartmentId), cancellationToken) == ids.Length;
        }
        catch (BusinessException exception) when (exception.ErrorCode is ErrorCode.Forbidden or ErrorCode.Unauthorized or ErrorCode.NotFound or ErrorCode.ValidationFailed)
        { return false; }
    }
}
