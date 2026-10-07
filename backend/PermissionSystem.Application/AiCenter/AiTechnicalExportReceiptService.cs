using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.AiCenter;

public sealed class AiTechnicalExportReceiptService(
    IRepository<OperationLog> logs, IAsyncQueryExecutor queries, AiTechnicalExportAccessPolicy accessPolicy,
    IDistributedRateLimitService rateLimits, TimeProvider time) : IAiTechnicalExportReceiptService
{
    private static readonly HashSet<string> FailureCodes = new(
        Enum.GetNames<ErrorCode>().Concat(["Cancelled", "PreparationTimeout", "InfrastructureFailure"]), StringComparer.Ordinal);

    public async Task<AiTechnicalExportReceiptPage> QueryAsync(AiTechnicalExportReceiptQuery request, CancellationToken ct = default)
    {
        if (request.PageIndex < 1 || request.PageSize is < 1 or > 50)
            throw new BusinessException(ErrorCode.ValidationFailed, "凭据分页范围无效，每页最多 50 条。");
        var snapshot = await ReadAsync(request, ct);
        var skip = (long)(request.PageIndex - 1) * request.PageSize;
        var page = skip >= snapshot.Groups.Count ? [] : snapshot.Groups.Skip((int)skip).Take(request.PageSize).Select(g => g.Summary).ToArray();
        return new(snapshot.Access.TargetTenantId, snapshot.From, snapshot.To, snapshot.ObservedFrom, snapshot.ObservedTo,
            snapshot.Matched, snapshot.Unreadable, PagedResult<AiTechnicalExportReceiptSummary>.Create(page, request.PageIndex, request.PageSize, snapshot.Groups.Count));
    }

    public async Task<AiTechnicalExportReceiptDetail> GetAsync(Guid exportId, AiTechnicalExportReceiptWindow request, CancellationToken ct = default)
    {
        var snapshot = await ReadAsync(request, ct);
        var group = snapshot.Groups.SingleOrDefault(g => g.Summary.ExportId == exportId)
            ?? throw new BusinessException(ErrorCode.NotFound, "导出凭据不可用。");
        return new(snapshot.Access.TargetTenantId, snapshot.From, snapshot.To, snapshot.ObservedFrom, snapshot.ObservedTo,
            snapshot.Matched, snapshot.Unreadable, group.Summary, group.Receipts);
    }

    private async Task<Snapshot> ReadAsync(AiTechnicalExportReceiptWindow request, CancellationToken cancellation)
    {
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        reading.CancelAfter(TimeSpan.FromSeconds(AiTechnicalExportReceiptContract.ReadSeconds));
        var ct = reading.Token;
        var observedFrom = time.GetUtcNow();
        var deadline = observedFrom.AddSeconds(AiTechnicalExportReceiptContract.ReadSeconds);
        var access = await accessPolicy.AuthorizeAsync(ct);
        var to = request.To ?? observedFrom;
        if (!request.From.HasValue && to < DateTimeOffset.MinValue.AddDays(30))
            throw InvalidWindow();
        var from = request.From ?? to.AddDays(-30);
        if (from >= to || to - from > TimeSpan.FromDays(90) || to > observedFrom.AddMinutes(5))
            throw InvalidWindow();
        await AdmitAsync("ai-export-receipts:actor", access.ActorId, 30, ct);
        await AdmitAsync("ai-export-receipts:tenant", access.TargetTenantId, 60, ct);
        IQueryable<Observation> Query() => logs.QueryForTenant(access.TargetTenantId)
            .Where(l => l.UserId == access.ActorId && l.CreatedAt >= from && l.CreatedAt < to &&
                l.Module == "AiTechnicalExport" && l.Method == AiCenterConstants.OperationsExportPermission &&
                l.RequestPath == AiTechnicalExportContract.Route && l.RequestMethod == "POST")
            .OrderBy(l => l.CreatedAt).ThenBy(l => l.Id)
            .Select(l => new Observation(l.Id, l.CreatedAt, l.Action, l.RequestBody));
        var original = await ReadBoundedAsync(Query(), ct);
        await accessPolicy.ReauthorizeAsync(access, ct);
        var parsed = new List<Parsed>();
        foreach (var row in original)
        {
            CheckDeadline();
            var entry = Parse(row);
            if (entry is not null) parsed.Add(entry);
        }
        var unreadable = original.Count - parsed.Count;
        var groups = parsed.GroupBy(p => p.ExportId).Select(g => Group(g.Key, g.Select(p => p.Receipt).ToArray(), unreadable))
            .OrderByDescending(g => g.Summary.LastRecordedAt).ThenBy(g => g.Summary.ExportId).ToArray();
        var current = await ReadBoundedAsync(Query(), ct);
        if (!original.SequenceEqual(current))
            throw new BusinessException(ErrorCode.Conflict, "凭据来源已变化，请重新查询。");
        await accessPolicy.ReauthorizeAsync(access, ct);
        CheckDeadline();
        return new(access, from, to, observedFrom, time.GetUtcNow(), original.Count, unreadable, groups);

        void CheckDeadline()
        {
            ct.ThrowIfCancellationRequested();
            if (time.GetUtcNow() >= deadline)
                throw new OperationCanceledException("AI export receipt reading expired.", ct);
        }
    }

    private static BusinessException InvalidWindow() => new(ErrorCode.ValidationFailed, "凭据记录时间范围无效，最多 90 天。");

    private async Task AdmitAsync(string policy, Guid key, int limit, CancellationToken ct)
    {
        if (!(await rateLimits.TryAcquireAsync(policy, key.ToString("N"), limit, TimeSpan.FromMinutes(1), ct)).IsAcquired)
            throw new BusinessException(ErrorCode.TooManyRequests, "导出凭据读取过于频繁，请稍后重试。");
    }

    private async Task<IReadOnlyList<Observation>> ReadBoundedAsync(IQueryable<Observation> query, CancellationToken ct)
    {
        var rows = await queries.ToListAsync(query.Take(AiTechnicalExportReceiptContract.MaxRecords + 1), ct);
        if (rows.Count > AiTechnicalExportReceiptContract.MaxRecords)
            throw new BusinessException(ErrorCode.ValidationFailed, AiTechnicalExportReceiptContract.CapacityMessage);
        return rows;
    }

    private static ReceiptGroup Group(Guid exportId, AiTechnicalExportReceipt[] entries, int unreadable)
    {
        var requested = entries.Count(r => r.Outcome == "Requested");
        var prepared = entries.Count(r => r.Outcome == "Prepared");
        var failed = entries.Count(r => r.Outcome == "Failed");
        var first = entries[0];
        var conflict = entries.Any(r => r.From != first.From || r.To != first.To || r.ObservedFrom != first.ObservedFrom);
        var reason = unreadable > 0 ? "WindowUnreadable" : conflict ? "ConflictingReceipts" :
            prepared == 0 ? "MissingPrepared" : prepared > 1 ? "MultiplePrepared" : "Ready";
        return new(new(exportId, entries.Min(r => r.RecordedAt), entries.Max(r => r.RecordedAt),
            requested, prepared, failed, reason == "Ready", reason), entries);
    }

    private static Parsed? Parse(Observation row)
    {
        if (row.Body is not { Length: > 0 and <= 4000 }) return null;
        try
        {
            using var json = JsonDocument.Parse(row.Body, new JsonDocumentOptions { MaxDepth = 8 });
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                root.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != root.EnumerateObject().Count()) return null;
            if (root.GetProperty("schemaVersion").GetInt32() != AiTechnicalExportContract.SchemaVersion ||
                root.GetProperty("purpose").GetString() != "TechnicalReview" || root.GetProperty("recipient").GetString() != "CurrentCaller") return null;
            var exportId = root.GetProperty("exportId").GetGuid();
            var outcome = root.GetProperty("outcome").GetString();
            if (exportId == Guid.Empty || outcome != row.Action || outcome is not ("Requested" or "Prepared" or "Failed")) return null;
            var from = root.GetProperty("from").GetDateTimeOffset();
            var to = root.GetProperty("to").GetDateTimeOffset();
            var observedFrom = root.GetProperty("observedFrom").GetDateTimeOffset();
            var observedTo = Optional(root, "observedTo", e => e.GetDateTimeOffset());
            var runs = Optional(root, "runCount", e => e.GetInt32());
            var usages = Optional(root, "usageCount", e => e.GetInt32());
            var bytes = Optional(root, "bytes", e => e.GetInt32());
            var hash = String(root, "fileSha256");
            var failure = String(root, "failureCode");
            if (from >= to || to - from > TimeSpan.FromDays(90) || to > observedFrom.AddMinutes(5) || observedTo < observedFrom) return null;
            if (outcome == "Prepared")
            {
                if (observedTo is null || runs is null or < 0 or > AiTechnicalExportContract.MaxRuns ||
                    usages is null or < 0 or > AiTechnicalExportContract.MaxUsages || bytes is null or <= 0 or > AiTechnicalExportContract.MaxBytes ||
                    hash is not { Length: 64 } || !hash.All(Uri.IsHexDigit) || failure is not null) return null;
            }
            else if (observedTo is not null || runs is not null || usages is not null || bytes is not null || hash is not null ||
                (outcome == "Requested" ? failure is not null : failure is null || !FailureCodes.Contains(failure))) return null;
            return new(exportId, new(row.Id, row.RecordedAt, 1, outcome, from, to, observedFrom, observedTo,
                runs, usages, bytes, hash?.ToUpperInvariant(), failure));
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or KeyNotFoundException or ArgumentOutOfRangeException)
        { return null; }
    }

    private static T? Optional<T>(JsonElement root, string name, Func<JsonElement, T> read) where T : struct =>
        root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? read(value) : null;
    private static string? String(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetString() : null;
    private sealed record Observation(Guid Id, DateTimeOffset RecordedAt, string Action, string? Body);
    private sealed record Parsed(Guid ExportId, AiTechnicalExportReceipt Receipt);
    private sealed record ReceiptGroup(AiTechnicalExportReceiptSummary Summary, IReadOnlyList<AiTechnicalExportReceipt> Receipts);
    private sealed record Snapshot(AiTechnicalExportAccess Access, DateTimeOffset From, DateTimeOffset To,
        DateTimeOffset ObservedFrom, DateTimeOffset ObservedTo, int Matched, int Unreadable, IReadOnlyList<ReceiptGroup> Groups);
}
