using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class Aic012TechnicalExportTests
{
    [Fact]
    public async Task Export_UsesWhitelistAndPersistsPreparationBeforeReturningExactHashedFile()
    {
        var f = new TechnicalExportFixture();
        var run = f.Core.AddRun(AiRunStatus.Completed, duration: 20);
        run.ErrorSummary = run.ExecutionConfigurationJson = run.ModelName = run.TraceId = "DO_NOT_EXPORT";
        var usage = f.Core.AddUsage(run, AiInvocationStatus.Completed, 10, null, 0, "USD");
        usage.ProviderRequestId = usage.ModelName = "DO_NOT_EXPORT"; usage.EstimatedOutputTokens = 20;
        var file = await f.Service().ExportAsync(f.Request);
        var text = Encoding.UTF8.GetString(file.Content);
        Assert.DoesNotContain("DO_NOT_EXPORT", text); Assert.DoesNotContain("actorUserId", text);
        Assert.DoesNotContain("conversationId", text); Assert.DoesNotContain("responseMessageId", text);
        using var json = JsonDocument.Parse(text);
        var payload = json.RootElement.GetProperty("payload");
        var manifest = json.RootElement.GetProperty("manifest");
        Assert.Equal(file.ExportId, manifest.GetProperty("exportId").GetGuid());
        Assert.Equal(TestIds.TenantId, manifest.GetProperty("tenantId").GetGuid());
        Assert.Equal("CurrentCaller", manifest.GetProperty("recipient").GetString());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload.GetRawText()))), manifest.GetProperty("payloadSha256").GetString());
        Assert.Equal(run.Id, payload.GetProperty("runs")[0].GetProperty("id").GetGuid());
        var row = payload.GetProperty("usages")[0];
        Assert.Equal(10, row.GetProperty("inputTokens").GetInt32());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("outputTokens").ValueKind);
        Assert.Equal(20, row.GetProperty("estimatedOutputTokens").GetInt32());
        Assert.True(row.GetProperty("usageIncomplete").GetBoolean());
        Assert.Equal(0, row.GetProperty("estimatedCost").GetDecimal());
        Assert.Equal(new[] { "Requested", "Prepared" }, f.Work.Persisted.Select(a => a.Action));
        Assert.All(f.Work.Persisted, a => { Assert.Equal(TestIds.TenantId, a.TenantId); Assert.Equal(f.Core.Current.UserId, a.UserId); Assert.Null(a.UserName); Assert.Null(a.ResponseBody); Assert.DoesNotContain("DO_NOT_EXPORT", a.RequestBody!); });
        using var receipt = JsonDocument.Parse(f.Work.Persisted.Last().RequestBody!);
        Assert.Equal(file.ExportId, receipt.RootElement.GetProperty("exportId").GetGuid());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(file.Content)), receipt.RootElement.GetProperty("fileSha256").GetString());
        Assert.Equal(file.Content.Length, receipt.RootElement.GetProperty("bytes").GetInt32());
        Assert.Equal(1, f.Locks.Released);
    }

    [Theory]
    [InlineData(AiInvocationStatus.Running, 2, "USD", true, false)]
    [InlineData(AiInvocationStatus.Failed, -1, "USD", false, true)]
    [InlineData(AiInvocationStatus.Completed, 2, "usd", false, true)]
    [InlineData((AiInvocationStatus)99, 2, "USD", false, false)]
    public async Task Export_SeparatesUnsettledUnknownAndInvalidValues(AiInvocationStatus status, int cost, string currency, bool unsettled, bool unknownCost)
    {
        var f = new TechnicalExportFixture(); var run = f.Core.AddRun((AiRunStatus)99, duration: -2);
        run.FallbackCount = -1;
        var usage = f.Core.AddUsage(run, status, -1, 0, cost, currency);
        usage.Sequence = -1; usage.Attempt = 0; usage.RouteRole = (AiModelRouteRole)99;
        using var doc = JsonDocument.Parse((await f.Service().ExportAsync(f.Request)).Content);
        var row = doc.RootElement.GetProperty("payload").GetProperty("usages")[0];
        Assert.Equal(JsonValueKind.Null, row.GetProperty("estimatedCost").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("inputTokens").ValueKind);
        Assert.Equal(0, row.GetProperty("outputTokens").GetInt32());
        Assert.True(row.GetProperty("invalidCounters").GetBoolean());
        Assert.Equal("Unknown", row.GetProperty("routeRole").GetString());
        Assert.Equal(unsettled, row.GetProperty("unsettled").GetBoolean());
        Assert.Equal(unknownCost, row.GetProperty("costUnknown").GetBoolean());
        var r = doc.RootElement.GetProperty("payload").GetProperty("runs")[0];
        Assert.Equal("Unknown", r.GetProperty("status").GetString()); Assert.True(r.GetProperty("invalidDuration").GetBoolean());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("fallbackCount").ValueKind);
    }

    [Fact]
    public async Task Export_UsesHalfOpenCreatedWindowAndExcludesWrongTenantAndDeletedAssociations()
    {
        var f = new TechnicalExportFixture(); var a = f.Core.AddRun(AiRunStatus.Pending); a.CreatedAt = f.Request.From!.Value;
        f.Core.AddRun(AiRunStatus.Completed).CreatedAt = f.Request.To!.Value;
        f.Core.AddRun(AiRunStatus.Completed).CreatedAt = f.Request.From.Value.AddTicks(-1);
        f.Core.AddRun(AiRunStatus.Completed).IsDeleted = true;
        f.Core.AddRun(AiRunStatus.Completed).TenantId = Guid.NewGuid();
        f.Core.AddUsage(a, AiInvocationStatus.Completed, 900, 0, 9, "USD").TenantId = Guid.NewGuid();
        f.Core.AddUsage(a, AiInvocationStatus.Completed, 900, 0, 9, "USD").IsDeleted = true;
        using var doc = JsonDocument.Parse((await f.Service().ExportAsync(f.Request)).Content);
        Assert.Equal(1, doc.RootElement.GetProperty("manifest").GetProperty("runCount").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("manifest").GetProperty("usageCount").GetInt32());
    }

    [Fact]
    public async Task Export_EmptyPopulationUsesDefaultWindowAndStillRequiresAudit()
    {
        var f = new TechnicalExportFixture();
        using var doc = JsonDocument.Parse((await f.Service().ExportAsync(new())).Content);
        var m = doc.RootElement.GetProperty("manifest");
        Assert.Equal(TimeSpan.FromDays(30), m.GetProperty("to").GetDateTimeOffset() - m.GetProperty("from").GetDateTimeOffset());
        Assert.Equal(0, m.GetProperty("runCount").GetInt32()); Assert.Equal(2, f.Work.Persisted.Count);
        await f.Service().ExportAsync(new() { From = f.Core.To.AddDays(-90), To = f.Core.To });
    }

    [Theory]
    [InlineData("view-only")]
    [InlineData("export-only")]
    [InlineData("revoked")]
    [InlineData("anonymous")]
    [InlineData("inactive")]
    [InlineData("other-tenant")]
    [InlineData("system")]
    [InlineData("no-target")]
    [InlineData("disabled-target")]
    [InlineData("stale-super")]
    public async Task Export_InvalidAuthorizationDoesNotReadOrWrite(string condition)
    {
        var f = new TechnicalExportFixture();
        switch (condition)
        {
            case "view-only": f.Core.Current = new(permissions: [AiCenterConstants.OperationsViewPermission]); break;
            case "export-only": f.Core.Current = new(permissions: [AiCenterConstants.OperationsExportPermission]); break;
            case "revoked": f.Core.Identities.Actor = f.Core.Identities.Actor! with { PermissionCodes = [AiCenterConstants.OperationsViewPermission] }; break;
            case "anonymous": f.Core.Current.UserId = null; break;
            case "inactive": f.Core.Identities.Actor = null; break;
            case "other-tenant": f.Core.Tenant.TenantId = Guid.NewGuid(); break;
            case "system": f.Core.Tenant.IsSystemScopeActive = true; break;
            case "no-target": f.Core.Tenant.TenantId = null; break;
            case "disabled-target": f.Core.Identities.ActiveTenants.Clear(); break;
            case "stale-super": f.Core.Current.IsSuperAdmin = true; break;
        }
        await Assert.ThrowsAsync<BusinessException>(() => f.Service().ExportAsync(f.Request));
        Assert.Equal(0, f.Core.Queries.ExecutionCount); Assert.Empty(f.Work.Persisted); Assert.Empty(f.Rates.Calls);
    }

    [Theory]
    [InlineData("reversed")]
    [InlineData("long")]
    [InlineData("future")]
    [InlineData("underflow")]
    public async Task Export_InvalidWindowDoesNotAdmitOrRead(string kind)
    {
        var f = new TechnicalExportFixture();
        var request = kind switch
        {
            "reversed" => new AiTechnicalExportRequest { From = f.Core.To, To = f.Core.From },
            "long" => new() { From = f.Core.To.AddDays(-91), To = f.Core.To },
            "future" => new() { To = DateTimeOffset.UtcNow.AddMinutes(6) },
            _ => new() { To = DateTimeOffset.MinValue }
        };
        Assert.Equal(ErrorCode.ValidationFailed, (await Assert.ThrowsAsync<BusinessException>(() => f.Service().ExportAsync(request))).ErrorCode);
        Assert.Empty(f.Rates.Calls); Assert.Empty(f.Work.Persisted);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Export_AuditPersistenceFailureNeverReturnsFile(int failAt)
    {
        var f = new TechnicalExportFixture(); f.Core.AddRun(AiRunStatus.Completed);
        f.Work.FailAt = failAt;
        await Assert.ThrowsAsync<IOException>(() => f.Service().ExportAsync(f.Request));
        Assert.Equal(failAt == 1 ? 0 : 1, f.Work.Persisted.Count);
        Assert.Equal(failAt == 1 ? 0 : 2, f.Core.Queries.MaterializedCounts.Count);
        Assert.Equal(1, f.Locks.Released);
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("stamp")]
    [InlineData("deleted")]
    [InlineData("moved-usage")]
    [InlineData("deleted-usage")]
    public async Task Export_ChangesAfterPreparedAuditPreventReturningFile(string change)
    {
        var f = new TechnicalExportFixture(); var run = f.Core.AddRun(AiRunStatus.Completed);
        var usage = f.Core.AddUsage(run, AiInvocationStatus.Completed, 1, 2, 1, "USD");
        f.Work.AfterSave = count =>
        {
            if (count != 2) return;
            switch (change)
            {
                case "revoked": f.Core.Identities.Actor = f.Core.Identities.Actor! with { PermissionCodes = [] }; break;
                case "stamp": f.Core.Identities.Actor = f.Core.Identities.Actor! with { SecurityStamp = Guid.NewGuid() }; break;
                case "deleted": run.IsDeleted = true; break;
                case "moved-usage": usage.RunId = Guid.NewGuid(); break;
                case "deleted-usage": usage.IsDeleted = true; break;
            }
        };
        await Assert.ThrowsAsync<BusinessException>(() => f.Service().ExportAsync(f.Request));
        Assert.Equal(new[] { "Requested", "Prepared", "Failed" }, f.Work.Persisted.Select(a => a.Action));
    }

    [Theory]
    [InlineData("runs")]
    [InlineData("usages")]
    [InlineData("bytes")]
    public async Task Export_CapacityRejectsInsteadOfTruncating(string kind)
    {
        var f = new TechnicalExportFixture(); var run = f.Core.AddRun(AiRunStatus.Completed);
        if (kind == "runs")
            for (var i = 0; i < AiTechnicalExportContract.MaxRuns; i++) f.Core.AddRun(AiRunStatus.Completed);
        else
            for (var i = 0; i < (kind == "bytes" ? 50_000 : 50_001); i++)
            { var u = f.Core.AddUsage(run, AiInvocationStatus.Completed, 1, 2, 1, "USD"); u.Sequence = i; }
        var error = await Assert.ThrowsAsync<BusinessException>(() => f.Service().ExportAsync(f.Request));
        Assert.Equal(AiTechnicalExportContract.CapacityMessage, error.Message);
        Assert.DoesNotContain(f.Work.Persisted, a => a.Action == "Prepared");
    }

    [Fact]
    public async Task Export_ExactlyTenThousandRunsAreNotRejectedAndOrderedStably()
    {
        var f = new TechnicalExportFixture();
        for (var i = 0; i < 10_000; i++) f.Core.AddRun(AiRunStatus.Pending).CreatedAt = f.Core.From.AddSeconds(10_000 - i);
        var file = await f.Service().ExportAsync(f.Request);
        using var doc = JsonDocument.Parse(file.Content);
        var rows = doc.RootElement.GetProperty("payload").GetProperty("runs");
        Assert.Equal(10_000, rows.GetArrayLength());
        Assert.True(rows[0].GetProperty("createdAt").GetDateTimeOffset() < rows[9999].GetProperty("createdAt").GetDateTimeOffset());
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("tenant")]
    [InlineData("busy")]
    [InlineData("expired-lock")]
    [InlineData("after-lock-revoke")]
    public async Task Export_AdmissionAndLockFailuresDoNotReadPopulation(string kind)
    {
        var f = new TechnicalExportFixture();
        if (kind == "actor" || kind == "tenant") f.Rates.DenyAt = kind == "actor" ? 1 : 2;
        if (kind == "busy") f.Locks.Busy = true;
        if (kind == "expired-lock") f.Locks.Expiry = TimeSpan.Zero;
        if (kind == "after-lock-revoke") f.Locks.AfterAcquire = () => f.Core.Identities.Actor = null;
        if (kind == "expired-lock")
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service().ExportAsync(f.Request));
        else
        {
            var error = await Assert.ThrowsAsync<BusinessException>(() => f.Service().ExportAsync(f.Request));
            Assert.Equal(kind is "actor" or "tenant" ? ErrorCode.TooManyRequests :
                kind == "busy" ? ErrorCode.Conflict : ErrorCode.Unauthorized, error.ErrorCode);
        }
        Assert.Empty(f.Work.Persisted); Assert.Equal(0, f.Core.Queries.ExecutionCount);
        Assert.Equal(kind == "actor" ? 1 : 2, f.Rates.Calls.Count);
    }

    [Fact]
    public async Task Export_CancellationAndDeadlineCannotReturnPreparedArtifact()
    {
        var f = new TechnicalExportFixture(); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service().ExportAsync(f.Request, cancel.Token));
        Assert.Empty(f.Work.Persisted);
        f.Core.AddRun(AiRunStatus.Completed);
        var clock = new ExportClock(); f.Clock = clock;
        f.Work.AfterSave = count => { if (count == 2) clock.Advance(TimeSpan.FromSeconds(11)); };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service().ExportAsync(f.Request));
        Assert.Equal("Failed", f.Work.Persisted.Last().Action);
    }

    [Theory]
    [InlineData("Header", true)]
    [InlineData("Request", true)]
    [InlineData("Identity", false)]
    public async Task Export_SuperAdministratorRequiresExplicitActiveSingleTarget(string source, bool allowed)
    {
        var f = new TechnicalExportFixture(); var target = Guid.NewGuid();
        var context = new TenantContext(); context.SetTenant(target, source); context.MarkAsSuperAdmin(true); f.Tenant = context;
        f.Core.Current.IsSuperAdmin = true;
        f.Core.Identities.Actor = f.Core.Identities.Actor! with { Roles = [SystemBuiltinConstants.SuperAdminRoleCode] };
        f.Core.Identities.ActiveTenants.Add(target);
        f.Core.AddRun(AiRunStatus.Completed);
        if (!allowed) { await Assert.ThrowsAsync<BusinessException>(() => f.Service().ExportAsync(f.Request)); return; }
        using var doc = JsonDocument.Parse((await f.Service().ExportAsync(f.Request)).Content);
        Assert.Equal(target, doc.RootElement.GetProperty("manifest").GetProperty("tenantId").GetGuid());
        Assert.Equal(0, doc.RootElement.GetProperty("manifest").GetProperty("runCount").GetInt32());
        Assert.All(f.Work.Persisted, a => Assert.Equal(target, a.TenantId));
    }
}

internal sealed class TechnicalExportFixture
{
    public Aic012Fixture Core { get; } = new();
    public InMemoryRepository<OperationLog> Logs { get; } = new();
    public ExportWork Work { get; }
    public ExportRates Rates { get; } = new();
    public ExportLocks Locks { get; } = new();
    public ITenantContext Tenant { get; set; }
    public TimeProvider Clock { get; set; } = TimeProvider.System;
    public AiTechnicalExportRequest Request => new() { From = Core.From, To = Core.To };
    public TechnicalExportFixture()
    {
        Core.Current = new(permissions: [AiCenterConstants.OperationsViewPermission, AiCenterConstants.OperationsExportPermission]);
        Core.Identities.Actor = Core.Identities.Actor! with { PermissionCodes = Core.Current.PermissionCodes };
        Tenant = Core.Tenant; Work = new(Logs);
    }
    public AiTechnicalExportService Service(IAsyncQueryExecutor? executor = null,
        IRepository<AiRun>? runs = null, IRepository<AiUsageLog>? usages = null) => new(
        runs ?? Core.Runs, usages ?? Core.Usages, Logs, executor ?? Core.Queries, Work,
        Tenant, new(Core.Current, Tenant, Core.Identities), Rates, Locks, Clock, NullLogger<AiTechnicalExportService>.Instance);

    internal sealed class ExportWork(InMemoryRepository<OperationLog> logs) : IUnitOfWork
    {
        public List<OperationLog> Persisted { get; } = [];
        public int SaveCount { get; private set; }
        public int? FailAt { get; set; }
        public Action<int>? AfterSave { get; set; }
        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested(); SaveCount++;
            if (FailAt.HasValue && SaveCount >= FailAt) throw new IOException("Synthetic audit failure");
            Persisted.AddRange(logs.Items.Except(Persisted)); AfterSave?.Invoke(SaveCount); return Task.FromResult(1);
        }
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default) => action(ct);
    }
    internal sealed class ExportRates : IDistributedRateLimitService
    {
        public List<(string Policy, string Key, int Limit)> Calls { get; } = [];
        public int DenyAt { get; set; }
        public Task<RateLimitAcquireResult> TryAcquireAsync(string policyName, string partitionKey, int permitLimit, TimeSpan window, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); Calls.Add((policyName, partitionKey, permitLimit)); return Task.FromResult(new RateLimitAcquireResult(Calls.Count != DenyAt, TimeSpan.FromMinutes(1))); }
    }
    internal sealed class ExportLocks : IDistributedLock
    {
        public bool Busy { get; set; }
        public TimeSpan? Expiry { get; set; }
        public Action? AfterAcquire { get; set; }
        public int Released { get; private set; }
        public Task<DistributedLockHandle?> TryAcquireAsync(string key, TimeSpan? expiry = null, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); AfterAcquire?.Invoke(); return Task.FromResult<DistributedLockHandle?>(Busy ? null : new(key, "Synthetic", Expiry ?? expiry!.Value)); }
        public Task<bool> ReleaseAsync(DistributedLockHandle handle, CancellationToken ct = default) { Released++; return Task.FromResult(true); }
        public Task<DistributedLockHandle> AcquireAsync(string key, TimeSpan? expiry = null, TimeSpan? waitTime = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ExecuteWithLockAsync(string key, Func<CancellationToken, Task> action, TimeSpan? expiry = null, TimeSpan? waitTime = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<T> ExecuteWithLockAsync<T>(string key, Func<CancellationToken, Task<T>> action, TimeSpan? expiry = null, TimeSpan? waitTime = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

internal sealed class ExportClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan delta) => _now += delta;
}
