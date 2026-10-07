using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiCenter;

public sealed class AiTechnicalExportService(
    IRepository<AiRun> runs, IRepository<AiUsageLog> usages, IRepository<OperationLog> audit,
    IAsyncQueryExecutor queries, IUnitOfWork unitOfWork, ITenantContext tenant,
    AiTechnicalExportAccessPolicy accessPolicy, IDistributedRateLimitService rateLimits, IDistributedLock locks,
    TimeProvider time, ILogger<AiTechnicalExportService> logger) : IAiTechnicalExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AiTechnicalExportFile> ExportAsync(AiTechnicalExportRequest request, CancellationToken cancellationToken = default)
    {
        using var preparation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        preparation.CancelAfter(TimeSpan.FromSeconds(AiTechnicalExportContract.PreparationSeconds));
        var ct = preparation.Token;
        var observedFrom = time.GetUtcNow();
        var deadline = observedFrom.AddSeconds(AiTechnicalExportContract.PreparationSeconds);
        var access = await accessPolicy.AuthorizeAsync(ct);
        var to = request.To ?? observedFrom;
        if (!request.From.HasValue && to < DateTimeOffset.MinValue.AddDays(30))
            throw new BusinessException(ErrorCode.ValidationFailed, "技术元数据导出时间范围无效。");
        var from = request.From ?? to.AddDays(-30);
        if (from >= to || to - from > TimeSpan.FromDays(90) || to > observedFrom.AddMinutes(5))
            throw new BusinessException(ErrorCode.ValidationFailed, "技术元数据导出时间范围无效，最多 90 天。");

        await AdmitAsync("ai-technical-export:actor", access.ActorId.ToString("N"), 2, ct);
        await AdmitAsync("ai-technical-export:tenant", access.TargetTenantId.ToString("N"), 10, ct);
        var handle = await locks.TryAcquireAsync($"ai-technical-export:{access.TargetTenantId:N}",
            TimeSpan.FromSeconds(AiTechnicalExportContract.LockSeconds), ct);
        if (handle is null)
            throw new BusinessException(ErrorCode.Conflict, "当前租户已有技术元数据正在准备导出，请稍后重试。");

        var exportId = Guid.NewGuid();
        var requested = false;
        try
        {
            CheckDeadline();
            await accessPolicy.ReauthorizeAsync(access, ct);
            await WriteAuditAsync(new(exportId, "Requested", from, to, observedFrom), access, ct);
            requested = true;
            var selectedRuns = await ReadBoundedAsync(runs.QueryForTenant(access.TargetTenantId)
                .Where(r => r.CreatedAt >= from && r.CreatedAt < to).OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
                .Select(r => new RunObservation(r.Id, r.ScenarioId, r.ScenarioVersionId, r.CreatedAt,
                    r.StartedAt, r.CompletedAt, r.Status, r.DurationMilliseconds, r.FallbackCount)),
                AiTechnicalExportContract.MaxRuns, ct);
            var runIds = selectedRuns.Select(r => r.Id).ToArray();
            IReadOnlyList<UsageObservation> selectedUsage = [];
            if (runIds.Length > 0)
                selectedUsage = await ReadBoundedAsync(
                    (from usage in usages.QueryForTenant(access.TargetTenantId)
                     join run in runs.QueryForTenant(access.TargetTenantId) on usage.RunId equals run.Id
                     where runIds.Contains(run.Id)
                     orderby usage.RunId, usage.Sequence, usage.Id
                     select new UsageObservation(usage.Id, usage.RunId, usage.CreatedAt, usage.Sequence, usage.Round,
                         usage.Attempt, usage.RouteRole, usage.Status, usage.StartedAt, usage.CompletedAt, usage.DurationMilliseconds,
                         usage.InputTokens, usage.OutputTokens, usage.EstimatedInputTokens, usage.EstimatedOutputTokens,
                         usage.EstimatedCost, usage.PricingCurrency)), AiTechnicalExportContract.MaxUsages, ct);

            CheckDeadline();
            var payload = new Payload(selectedRuns.Select(ToExport).ToArray(), selectedUsage.Select(ToExport).ToArray());
            using var payloadBuffer = new BoundedBuffer(ct);
            await JsonSerializer.SerializeAsync(payloadBuffer, payload, JsonOptions, ct);
            var payloadBytes = payloadBuffer.GetBuffer().AsMemory(0, checked((int)payloadBuffer.Length));
            var manifest = new AiTechnicalExportManifest(AiTechnicalExportContract.SchemaVersion, exportId,
                access.TargetTenantId, from, to, observedFrom, time.GetUtcNow(), selectedRuns.Count, selectedUsage.Count,
                Convert.ToHexString(SHA256.HashData(payloadBytes.Span)));
            using var fileBuffer = new BoundedBuffer(ct);
            using (var writer = new Utf8JsonWriter(fileBuffer))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("manifest");
                JsonSerializer.Serialize(writer, manifest, JsonOptions);
                writer.WritePropertyName("payload");
                writer.WriteRawValue(payloadBytes.Span);
                writer.WriteEndObject();
                await writer.FlushAsync(ct);
            }
            var bytes = fileBuffer.ToArray();
            await RecheckSourcesAsync(access.TargetTenantId, runIds, selectedUsage, ct);
            await accessPolicy.ReauthorizeAsync(access, ct);
            CheckDeadline();
            await WriteAuditAsync(new(exportId, "Prepared", from, to, observedFrom, manifest.ObservedTo,
                selectedRuns.Count, selectedUsage.Count, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes))), access, ct);
            await RecheckSourcesAsync(access.TargetTenantId, runIds, selectedUsage, ct);
            await accessPolicy.ReauthorizeAsync(access, ct);
            CheckDeadline();
            return new(exportId, bytes);
        }
        catch (Exception error)
        {
            if (requested && tenant.TenantId == access.TargetTenantId && !tenant.IsSystemScopeActive)
            {
                using var failureAudit = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                try
                {
                    var code = error is BusinessException business ? business.ErrorCode.ToString() :
                        error is OperationCanceledException ? (cancellationToken.IsCancellationRequested ? "Cancelled" : "PreparationTimeout") : "InfrastructureFailure";
                    await WriteAuditAsync(new(exportId, "Failed", from, to, observedFrom, FailureCode: code), access, failureAudit.Token);
                }
                catch (Exception)
                {
                    logger.LogWarning("AI technical export final audit could not be persisted; the requested record is not evidence of delivery.");
                }
            }
            throw;
        }
        finally
        {
            using var release = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            try { await locks.ReleaseAsync(handle, release.Token); }
            catch (Exception) { logger.LogWarning("AI technical export preparation lock could not be released; its lease will expire."); }
        }

        void CheckDeadline()
        {
            ct.ThrowIfCancellationRequested();
            if (time.GetUtcNow() >= deadline || time.GetUtcNow() >= handle.AcquiredAt.Add(handle.Expiry))
                throw new OperationCanceledException("AI technical export preparation expired.", ct);
        }
    }

    private async Task AdmitAsync(string policy, string key, int limit, CancellationToken ct)
    {
        if (!(await rateLimits.TryAcquireAsync(policy, key, limit, TimeSpan.FromMinutes(1), ct)).IsAcquired)
            throw new BusinessException(ErrorCode.TooManyRequests, "技术元数据导出过于频繁，请稍后重试。");
    }

    private async Task<IReadOnlyList<T>> ReadBoundedAsync<T>(IQueryable<T> query, int limit, CancellationToken ct)
    {
        var rows = await queries.ToListAsync(query.Take(limit + 1), ct);
        if (rows.Count > limit) throw new BusinessException(ErrorCode.ValidationFailed, AiTechnicalExportContract.CapacityMessage);
        return rows;
    }

    private async Task RecheckSourcesAsync(Guid target, Guid[] runIds, IReadOnlyList<UsageObservation> selected, CancellationToken ct)
    {
        if (runIds.Length > 0 && await queries.LongCountAsync(runs.QueryForTenant(target).Where(r => runIds.Contains(r.Id)), ct) != runIds.LongLength)
            throw new BusinessException(ErrorCode.Conflict, "导出源数据已变化，请重新导出。");
        if (selected.Count == 0) return;
        var ids = selected.Select(u => u.Id).ToArray();
        var links = await ReadBoundedAsync(
            from usage in usages.QueryForTenant(target)
            join run in runs.QueryForTenant(target) on usage.RunId equals run.Id
            where ids.Contains(usage.Id) && runIds.Contains(run.Id)
            select new UsageLink(usage.Id, usage.RunId), AiTechnicalExportContract.MaxUsages, ct);
        var original = selected.ToDictionary(u => u.Id, u => u.RunId);
        if (links.Count != original.Count || links.Any(u => !original.TryGetValue(u.Id, out var runId) || runId != u.RunId))
            throw new BusinessException(ErrorCode.Conflict, "导出用量归属已变化，请重新导出。");
    }

    private async Task WriteAuditAsync(AuditSummary summary, AiTechnicalExportAccess access, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var body = JsonSerializer.Serialize(summary, JsonOptions);
        if (body.Length > 4000) throw new InvalidOperationException("AI export audit summary exceeded its contract.");
        await audit.AddAsync(new OperationLog
        {
            Id = Guid.NewGuid(), TenantId = access.TargetTenantId, UserId = access.ActorId,
            Module = "AiTechnicalExport", Action = summary.Outcome, Method = AiCenterConstants.OperationsExportPermission,
            RequestPath = AiTechnicalExportContract.Route, RequestMethod = "POST", RequestBody = body,
            StatusCode = summary.Outcome == "Prepared" ? 200 : summary.Outcome == "Requested" ? 202 : 0
        }, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static AiTechnicalRunExport ToExport(RunObservation r) => new(r.Id, r.ScenarioId, r.ScenarioVersionId,
        r.CreatedAt, r.StartedAt, r.CompletedAt, Name(r.Status), Nonnegative(r.Duration),
        r.FallbackCount >= 0 ? r.FallbackCount : null, r.Duration is < 0, r.FallbackCount < 0);

    private static AiTechnicalUsageExport ToExport(UsageObservation u)
    {
        var settled = u.Status is AiInvocationStatus.Completed or AiInvocationStatus.Failed or AiInvocationStatus.Cancelled;
        var knownCost = settled && u.Cost is >= 0 && u.Currency is { Length: 3 } && u.Currency.All(c => c is >= 'A' and <= 'Z');
        return new(u.Id, u.RunId, u.CreatedAt, u.Sequence >= 0 ? u.Sequence : null, u.Round >= 0 ? u.Round : null,
            u.Attempt >= 1 ? u.Attempt : null, Name(u.Role), Name(u.Status), u.StartedAt, u.CompletedAt,
            Nonnegative(u.Duration), Nonnegative(u.Input), Nonnegative(u.Output), Nonnegative(u.EstimatedInput), Nonnegative(u.EstimatedOutput),
            knownCost ? u.Cost : null, knownCost ? u.Currency : null, u.Input is null or < 0 || u.Output is null or < 0,
            settled && !knownCost, u.Status is AiInvocationStatus.Pending or AiInvocationStatus.Running,
            u.Duration is < 0, u.Sequence < 0 || u.Round < 0 || u.Attempt < 1,
            u.Input is < 0 || u.Output is < 0, u.EstimatedInput is < 0 || u.EstimatedOutput is < 0);
    }

    private static string Name<T>(T value) where T : struct, Enum => Enum.IsDefined(value) ? value.ToString() : "Unknown";
    private static int? Nonnegative(int? value) => value is >= 0 ? value : null;
    private static long? Nonnegative(long? value) => value is >= 0 ? value : null;
    private sealed record RunObservation(Guid Id, Guid? ScenarioId, Guid? ScenarioVersionId, DateTimeOffset CreatedAt,
        DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, AiRunStatus Status, long? Duration, int FallbackCount);
    private sealed record UsageObservation(Guid Id, Guid RunId, DateTimeOffset CreatedAt, int Sequence, int Round, int Attempt,
        AiModelRouteRole Role, AiInvocationStatus Status, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt,
        long? Duration, int? Input, int? Output, int? EstimatedInput, int? EstimatedOutput, decimal? Cost, string? Currency);
    private sealed record UsageLink(Guid Id, Guid RunId);
    private sealed record Payload(IReadOnlyList<AiTechnicalRunExport> Runs, IReadOnlyList<AiTechnicalUsageExport> Usages)
    {
        public int SchemaVersion => AiTechnicalExportContract.SchemaVersion;
    }
    private sealed record AuditSummary(Guid ExportId, string Outcome, DateTimeOffset From, DateTimeOffset To,
        DateTimeOffset ObservedFrom, DateTimeOffset? ObservedTo = null, int? RunCount = null, int? UsageCount = null,
        int? Bytes = null, string? FileSha256 = null, string? FailureCode = null)
    {
        public int SchemaVersion => AiTechnicalExportContract.SchemaVersion;
        public string Purpose => "TechnicalReview";
        public string Recipient => "CurrentCaller";
    }

    private sealed class BoundedBuffer(CancellationToken cancellation) : MemoryStream
    {
        private void Check(int count)
        {
            cancellation.ThrowIfCancellationRequested();
            if (Position + count > AiTechnicalExportContract.MaxBytes)
                throw new BusinessException(ErrorCode.ValidationFailed, AiTechnicalExportContract.CapacityMessage);
        }
        public override void Write(byte[] buffer, int offset, int count) { Check(count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); base.Write(buffer); }
        public override void WriteByte(byte value) { Check(1); base.WriteByte(value); }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); Write(buffer, offset, count); return Task.CompletedTask; }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); Write(buffer.Span); return ValueTask.CompletedTask; }
    }
}
