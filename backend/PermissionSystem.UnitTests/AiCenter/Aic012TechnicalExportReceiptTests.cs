using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class Aic012TechnicalExportReceiptTests
{
    [Fact]
    public async Task Query_GroupsStageFactsWithoutClaimingDeliveryAndReturnsOnlySafeMetadata()
    {
        var f = new ReceiptFixture(); var id = Guid.NewGuid();
        f.Add(id, "Requested"); var prepared = f.Add(id, "Prepared"); f.Add(id, "Failed");
        prepared.UserName = prepared.TraceId = prepared.IpAddress = prepared.ResponseBody = "DO_NOT_RETURN";
        var page = await f.Service().QueryAsync(f.Request);
        var group = Assert.Single(page.Exports.Items);
        Assert.Equal(1, group.RequestedCount); Assert.Equal(1, group.PreparedCount); Assert.Equal(1, group.FailedCount);
        Assert.True(group.CanVerify); Assert.Equal("Ready", group.VerificationReason);
        Assert.Equal("CurrentCaller", page.Scope); Assert.Equal(3, page.MatchedRecordCount);
        var detail = await f.Service().GetAsync(id, f.Request);
        Assert.Equal(3, detail.Receipts.Count); Assert.Equal(prepared.Id, detail.Receipts.Single(r => r.Outcome == "Prepared").ReceiptId);
        Assert.Equal(new string('A', 64), detail.Receipts.Single(r => r.Outcome == "Prepared").FileSha256);
        var json = JsonSerializer.Serialize(detail, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var field in new[] { "DO_NOT_RETURN", "userId", "userName", "requestBody", "responseBody", "ipAddress", "traceId", "delivered" }) Assert.DoesNotContain(field, json);
        Assert.Empty(f.Export.Work.Persisted);
    }

    [Fact]
    public async Task Query_ReadsActualFirstBatchV1ReceiptsWithoutChangingItsFileContract()
    {
        var f = new ReceiptFixture(); var file = await f.Export.Service().ExportAsync(new());
        foreach (var log in f.Export.Logs.Items) log.CreatedAt = f.Observed;
        var detail = await f.Service().GetAsync(file.ExportId, f.Request);
        var prepared = Assert.Single(detail.Receipts, r => r.Outcome == "Prepared");
        Assert.True(detail.Export.CanVerify); Assert.Equal(file.Content.Length, prepared.Bytes);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file.Content)), prepared.FileSha256);
    }

    [Theory]
    [InlineData("other-actor")]
    [InlineData("other-tenant")]
    [InlineData("deleted")]
    [InlineData("module")]
    [InlineData("method")]
    [InlineData("path")]
    [InlineData("verb")]
    [InlineData("upper-bound")]
    public async Task Query_ExcludesInvisibleAndNonDedicatedRowsAndDetailDoesNotRevealExistence(string change)
    {
        var f = new ReceiptFixture(); var id = Guid.NewGuid(); var row = f.Add(id, "Prepared");
        switch (change)
        {
            case "other-actor": row.UserId = Guid.NewGuid(); break;
            case "other-tenant": row.TenantId = Guid.NewGuid(); break;
            case "deleted": row.IsDeleted = true; break;
            case "module": row.Module = "AiOperations"; break;
            case "method": row.Method = "other"; break;
            case "path": row.RequestPath += "/other"; break;
            case "verb": row.RequestMethod = "GET"; break;
            case "upper-bound": row.CreatedAt = f.Request.To!.Value; break;
        }
        Assert.Empty((await f.Service().QueryAsync(f.Request)).Exports.Items);
        Assert.Equal(ErrorCode.NotFound, (await Assert.ThrowsAsync<BusinessException>(() => f.Service().GetAsync(id, f.Request))).ErrorCode);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("length")]
    [InlineData("schema")]
    [InlineData("duplicate")]
    [InlineData("unknown-outcome")]
    [InlineData("action-mismatch")]
    [InlineData("purpose")]
    [InlineData("recipient")]
    [InlineData("export-id")]
    [InlineData("hash")]
    [InlineData("run-count")]
    [InlineData("usage-count")]
    [InlineData("bytes")]
    [InlineData("time")]
    [InlineData("observed-time")]
    [InlineData("type")]
    [InlineData("missing")]
    [InlineData("failure-code")]
    public async Task Query_UnreadableWindowDisablesVerificationWithoutEchoingBadJson(string kind)
    {
        var f = new ReceiptFixture(); f.Add(Guid.NewGuid(), "Prepared"); var bad = f.Add(Guid.NewGuid(), kind == "failure-code" ? "Failed" : "Prepared");
        var body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(bad.RequestBody!)!;
        void Set(string key, object? value) => body[key] = JsonSerializer.SerializeToElement(value);
        switch (kind)
        {
            case "schema": Set("schemaVersion", 2); break;
            case "unknown-outcome": Set("outcome", "DO_NOT_RETURN"); bad.Action = "DO_NOT_RETURN"; break;
            case "action-mismatch": bad.Action = "Failed"; break;
            case "purpose": Set("purpose", "DO_NOT_RETURN"); break;
            case "recipient": Set("recipient", "DO_NOT_RETURN"); break;
            case "export-id": Set("exportId", Guid.Empty); break;
            case "hash": Set("fileSha256", "DO_NOT_RETURN"); break;
            case "run-count": Set("runCount", 10001); break;
            case "usage-count": Set("usageCount", -1); break;
            case "bytes": Set("bytes", 0); break;
            case "time": Set("from", f.Observed.AddDays(1)); break;
            case "observed-time": Set("observedTo", f.Observed.AddSeconds(-1)); break;
            case "type": Set("bytes", "DO_NOT_RETURN"); break;
            case "missing": body.Remove("from"); break;
            case "failure-code": Set("failureCode", "DO_NOT_RETURN"); break;
        }
        bad.RequestBody = JsonSerializer.Serialize(body);
        if (kind == "json") bad.RequestBody = "DO_NOT_RETURN";
        if (kind == "length") bad.RequestBody = new string('X', 4001);
        if (kind == "duplicate") bad.RequestBody = bad.RequestBody[..^1] + ",\"schemaVersion\":1}";
        var response = await f.Service().QueryAsync(f.Request);
        Assert.Equal(1, response.UnreadableRecordCount); Assert.False(response.WindowInterpretable);
        Assert.Equal("WindowUnreadable", Assert.Single(response.Exports.Items).VerificationReason);
        Assert.DoesNotContain("DO_NOT_RETURN", JsonSerializer.Serialize(response));
    }

    [Theory]
    [InlineData("missing", "MissingPrepared")]
    [InlineData("duplicate", "MultiplePrepared")]
    [InlineData("conflict", "ConflictingReceipts")]
    public async Task Query_DoesNotChooseLatestReceiptOrInventMissingStages(string kind, string reason)
    {
        var f = new ReceiptFixture(); var id = Guid.NewGuid(); f.Add(id, "Requested");
        if (kind != "missing") f.Add(id, "Prepared");
        if (kind == "duplicate") f.Add(id, "Prepared");
        if (kind == "conflict")
        {
            var row = f.Add(id, "Failed");
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.RequestBody!)!;
            fields["from"] = JsonSerializer.SerializeToElement(f.Observed.AddDays(-2));
            row.RequestBody = JsonSerializer.Serialize(fields);
        }
        var group = Assert.Single((await f.Service().QueryAsync(f.Request)).Exports.Items);
        Assert.False(group.CanVerify); Assert.Equal(reason, group.VerificationReason);
    }

    [Fact]
    public async Task Query_HalfOpenWindowStablePaginationAndHugePageDoesNotOverflow()
    {
        var f = new ReceiptFixture(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        f.Add(first, "Prepared").CreatedAt = f.Request.From!.Value;
        f.Add(second, "Prepared").CreatedAt = f.Observed;
        var page = await f.Service().QueryAsync(new() { From = f.Request.From, To = f.Request.To, PageIndex = 2, PageSize = 1 });
        Assert.Equal(first, Assert.Single(page.Exports.Items).ExportId); Assert.Equal(2, page.Exports.TotalCount);
        Assert.Empty((await f.Service().QueryAsync(new() { From = f.Request.From, To = f.Request.To, PageIndex = int.MaxValue, PageSize = 50 })).Exports.Items);
        var defaults = await new ReceiptFixture().Service().QueryAsync(new());
        Assert.Equal(TimeSpan.FromDays(30), defaults.ReceiptTo - defaults.ReceiptFrom);
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public async Task Query_EnforcesWholeWindowCapacity(int count, bool allowed)
    {
        var f = new ReceiptFixture(); for (var i = 0; i < count; i++) f.Add(Guid.NewGuid(), "Requested");
        if (allowed) Assert.Equal(count, (await f.Service().QueryAsync(f.Request)).MatchedRecordCount);
        else Assert.Equal(AiTechnicalExportReceiptContract.CapacityMessage, (await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryAsync(f.Request))).Message);
    }

    [Theory]
    [InlineData("view-only")]
    [InlineData("export-only")]
    [InlineData("revoked")]
    [InlineData("anonymous")]
    [InlineData("system")]
    [InlineData("inactive-target")]
    public async Task Query_InvalidAccessDoesNotRead(string kind)
    {
        var f = new ReceiptFixture(); var core = f.Export.Core;
        if (kind == "view-only") core.Current = new(permissions: [AiCenterConstants.OperationsViewPermission]);
        if (kind == "export-only") core.Current = new(permissions: [AiCenterConstants.OperationsExportPermission]);
        if (kind == "revoked") core.Identities.Actor = core.Identities.Actor! with { PermissionCodes = [] };
        if (kind == "anonymous") core.Current.UserId = null;
        if (kind == "system") core.Tenant.IsSystemScopeActive = true;
        if (kind == "inactive-target") core.Identities.ActiveTenants.Clear();
        await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryAsync(f.Request));
        Assert.Equal(0, core.Queries.ExecutionCount); Assert.Empty(f.Export.Rates.Calls);
    }

    [Theory]
    [InlineData("body")]
    [InlineData("deleted")]
    [InlineData("actor")]
    [InlineData("new-row")]
    [InlineData("revoked")]
    [InlineData("stamp")]
    public async Task Query_ChangesDuringReadingPreventReturn(string kind)
    {
        var f = new ReceiptFixture(); var row = f.Add(Guid.NewGuid(), "Prepared");
        f.Export.Core.Queries.AfterList = count =>
        {
            if (count != 1) return;
            if (kind == "body") row.RequestBody += " ";
            if (kind == "deleted") row.IsDeleted = true;
            if (kind == "actor") row.UserId = Guid.NewGuid();
            if (kind == "new-row") f.Add(Guid.NewGuid(), "Requested");
            if (kind == "revoked") f.Export.Core.Identities.Actor = f.Export.Core.Identities.Actor! with { PermissionCodes = [] };
            if (kind == "stamp") f.Export.Core.Identities.Actor = f.Export.Core.Identities.Actor! with { SecurityStamp = Guid.NewGuid() };
        };
        var error = await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryAsync(f.Request));
        Assert.Equal(kind == "stamp" ? ErrorCode.Forbidden : kind == "revoked" ? ErrorCode.Forbidden : ErrorCode.Conflict, error.ErrorCode);
    }

    [Fact]
    public async Task Query_RejectsInvalidWindowPaginationRateCancellationAndDeadline()
    {
        var f = new ReceiptFixture();
        foreach (var request in new AiTechnicalExportReceiptQuery[] { new() { From = f.Observed, To = f.Observed }, new() { From = f.Observed.AddDays(-91), To = f.Observed }, new() { To = DateTimeOffset.MinValue }, new() { To = DateTimeOffset.UtcNow.AddMinutes(6) }, new() { PageSize = 51 }, new() { PageIndex = 0 } })
            Assert.Equal(ErrorCode.ValidationFailed, (await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryAsync(request))).ErrorCode);
        Assert.Empty(f.Export.Rates.Calls);
        f.Export.Rates.DenyAt = 1;
        Assert.Equal(ErrorCode.TooManyRequests, (await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryAsync(f.Request))).ErrorCode);
        using var ct = new CancellationTokenSource(); ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ReceiptFixture().Service().QueryAsync(new(), ct.Token));
        var timed = new ReceiptFixture(); var clock = new ExportClock(); timed.Export.Clock = clock;
        timed.Export.Core.Queries.AfterList = _ => clock.Advance(TimeSpan.FromSeconds(11));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => timed.Service().QueryAsync(timed.Request));
    }

    [Fact]
    public async Task Query_SuperAdminStillReadsOnlySelfWithinExplicitTarget()
    {
        var f = new ReceiptFixture(); var target = Guid.NewGuid(); var tenant = new TenantContext(); tenant.SetTenant(target, "Header");
        f.Export.Tenant = tenant; f.Export.Core.Current.IsSuperAdmin = true;
        f.Export.Core.Identities.Actor = f.Export.Core.Identities.Actor! with { Roles = [SystemBuiltinConstants.SuperAdminRoleCode] };
        f.Export.Core.Identities.ActiveTenants.Add(target);
        var mine = f.Add(Guid.NewGuid(), "Prepared"); mine.TenantId = target;
        var other = f.Add(Guid.NewGuid(), "Prepared"); other.TenantId = target; other.UserId = Guid.NewGuid();
        Assert.Equal(1, (await f.Service().QueryAsync(f.Request)).MatchedRecordCount);
        tenant.SetTenant(target, "Identity");
        await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryAsync(f.Request));
    }
}

internal sealed class ReceiptFixture
{
    public TechnicalExportFixture Export { get; } = new();
    public DateTimeOffset Observed { get; } = DateTimeOffset.UtcNow.AddMinutes(-1);
    public AiTechnicalExportReceiptQuery Request => new() { From = Observed.AddDays(-1), To = Observed.AddMinutes(2) };
    public AiTechnicalExportReceiptService Service(IAsyncQueryExecutor? executor = null, IRepository<OperationLog>? logs = null) => new(
        logs ?? Export.Logs, executor ?? Export.Core.Queries, new(Export.Core.Current, Export.Tenant, Export.Core.Identities), Export.Rates, Export.Clock);
    public OperationLog Add(Guid id, string outcome)
    {
        var body = new Dictionary<string, object?>
        {
            ["exportId"] = id, ["outcome"] = outcome, ["from"] = Observed.AddDays(-1).ToString("O"), ["to"] = Observed.ToString("O"),
            ["observedFrom"] = Observed.ToString("O"), ["observedTo"] = outcome == "Prepared" ? Observed.AddSeconds(1).ToString("O") : null,
            ["runCount"] = outcome == "Prepared" ? 1 : null, ["usageCount"] = outcome == "Prepared" ? 2 : null,
            ["bytes"] = outcome == "Prepared" ? 100 : null, ["fileSha256"] = outcome == "Prepared" ? new string('a', 64) : null,
            ["failureCode"] = outcome == "Failed" ? "Cancelled" : null, ["schemaVersion"] = 1, ["purpose"] = "TechnicalReview", ["recipient"] = "CurrentCaller"
        };
        var row = new OperationLog { Id = Guid.NewGuid(), TenantId = Export.Core.Tenant.TenantId!.Value, UserId = Export.Core.Current.UserId,
            CreatedAt = Observed, Module = "AiTechnicalExport", Method = AiCenterConstants.OperationsExportPermission, RequestMethod = "POST",
            RequestPath = AiTechnicalExportContract.Route, Action = outcome, RequestBody = JsonSerializer.Serialize(body) };
        Export.Logs.AddAsync(row).GetAwaiter().GetResult(); return row;
    }
}
