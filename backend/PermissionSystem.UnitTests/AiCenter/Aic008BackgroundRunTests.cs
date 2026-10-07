using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Ai;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Infrastructure.Queries;
using PermissionSystem.Infrastructure.Repositories;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class Aic008BackgroundRunTests
{
    [Fact]
    public async Task Claim_ShouldExecuteOnlyPendingBackgroundRunOnceAndSeparateQueueFromExecutionTime()
    {
        await using var fixture = await Fixture.CreateAsync();
        var run = await fixture.Store.ClaimAsync(fixture.RunId);
        Assert.NotNull(run);
        Assert.Equal(AiRunStatus.Running, run.Status);
        Assert.NotEqual(fixture.OriginalLease, run.ExecutionLeaseId);
        Assert.True(run.DeadlineAt > DateTimeOffset.UtcNow.AddSeconds(80));
        Assert.Null(await fixture.Store.ClaimAsync(fixture.RunId));
        Assert.Empty(fixture.Db.AiUsageLogs);
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("expired")]
    [InlineData("cancelled")]
    [InlineData("deleted")]
    public async Task Claim_ShouldRejectUnrecoverableQueueEntries(string condition)
    {
        await using var fixture = await Fixture.CreateAsync();
        var row = await fixture.Db.AiRuns.SingleAsync();
        switch (condition)
        {
            case "legacy": row.ExecutionMode = null; break;
            case "expired": row.QueueDeadlineAt = DateTimeOffset.UtcNow.AddSeconds(-1); break;
            case "cancelled": row.CancellationRequestedAt = DateTimeOffset.UtcNow; break;
            case "deleted": row.IsDeleted = true; break;
        }
        await fixture.Db.SaveChangesAsync(); fixture.Db.ChangeTracker.Clear();
        Assert.Null(await fixture.Store.ClaimAsync(fixture.RunId));
    }

    [Fact]
    public async Task PendingCancel_ShouldBeTerminalWithoutUsageAndCannotBeClaimed()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Store.CancelAsync(fixture.RunId);
        await fixture.Store.CancelAsync(fixture.RunId);
        var row = await fixture.Store.FindAsync(fixture.RunId);
        Assert.Equal(AiRunStatus.Cancelled, row!.Status);
        Assert.Null(await fixture.Store.ClaimAsync(fixture.RunId));
        Assert.Empty(fixture.Db.AiUsageLogs);
    }

    [Fact]
    public async Task Heartbeat_ShouldNotLoseCancellationOrExtendAbsoluteDeadline()
    {
        await using var fixture = await Fixture.CreateAsync();
        var run = (await fixture.Store.ClaimAsync(fixture.RunId))!;
        Assert.True(await fixture.Store.HeartbeatAsync(run.Id, run.ExecutionLeaseId));
        Assert.Equal(run.DeadlineAt, (await fixture.Store.FindAsync(run.Id))!.DeadlineAt);
        await fixture.Store.CancelAsync(run.Id);
        Assert.False(await fixture.Store.HeartbeatAsync(run.Id, run.ExecutionLeaseId));
        Assert.NotNull((await fixture.Store.FindAsync(run.Id))!.CancellationRequestedAt);
    }

    [Fact]
    public async Task WatchdogTermination_ShouldSettleUnknownUsageOnceAndFinishTools()
    {
        await using var fixture = await Fixture.CreateAsync();
        var run = (await fixture.Store.ClaimAsync(fixture.RunId))!;
        fixture.Db.AiUsageLogs.Add(new() { RunId = run.Id, TenantId = run.TenantId, ModelName = "synthetic",
            Status = AiInvocationStatus.Running, ReservedCost = .005m, EstimatedInputTokens = 1000,
            EstimatedOutputTokens = 2000, InputTokenPricePerMillion = 1, OutputTokenPricePerMillion = 2, PricingCurrency = "XXX" });
        fixture.Db.AiToolInvocations.Add(new() { RunId = run.Id, TenantId = run.TenantId, InvocationId = "synthetic-call",
            Status = AiInvocationStatus.Running });
        await fixture.Db.SaveChangesAsync(); fixture.Db.ChangeTracker.Clear();
        await fixture.Store.TerminateAsync(run.Id, run.ExecutionLeaseId, "worker_stopped");
        await fixture.Store.TerminateAsync(run.Id, run.ExecutionLeaseId, "worker_stopped");
        var usage = await fixture.Db.AiUsageLogs.SingleAsync();
        Assert.Equal(.005m, usage.EstimatedCost); Assert.Null(usage.ReservedCost); Assert.Null(usage.InputTokens);
        Assert.Equal(AiInvocationStatus.Failed, (await fixture.Db.AiToolInvocations.SingleAsync()).Status);
        Assert.NotEqual(run.ExecutionLeaseId, (await fixture.Store.FindAsync(run.Id))!.ExecutionLeaseId);
    }

    [Fact]
    public async Task LateResult_ShouldNotPersistMessagesAfterLeaseLoss()
    {
        await using var fixture = await Fixture.CreateAsync();
        var run = (await fixture.Store.ClaimAsync(fixture.RunId))!;
        await fixture.Store.TerminateAsync(run.Id, run.ExecutionLeaseId, "worker_stopped");
        fixture.Fence.Begin(run.Id, run.ExecutionLeaseId);
        fixture.Db.AiMessages.Add(new() { TenantId = run.TenantId, ConversationId = run.ConversationId,
            Role = AiMessageRole.Assistant, Content = "late-result" });
        await Assert.ThrowsAsync<AiRunLeaseLostException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.ChangeTracker.Clear();
        Assert.Empty(fixture.Db.AiMessages);
    }

    [Fact]
    public async Task Cancellation_ShouldRejectToolResultAndSettlementDiscardUnsavedContent()
    {
        await using var fixture = await Fixture.CreateAsync();
        var run = (await fixture.Store.ClaimAsync(fixture.RunId))!;
        await fixture.Store.CancelAsync(run.Id);
        fixture.Fence.Begin(run.Id, run.ExecutionLeaseId);
        fixture.Db.AiMessages.Add(new() { TenantId = run.TenantId, ConversationId = run.ConversationId,
            Role = AiMessageRole.Tool, Content = "uncommitted-result" });
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Db.SaveChangesAsync());
        fixture.Fence.IsSettlement = true;
        var tracked = await fixture.Db.AiRuns.SingleAsync(); tracked.Status = AiRunStatus.Failed;
        await fixture.Db.SaveChangesAsync(); fixture.Db.ChangeTracker.Clear();
        Assert.Empty(fixture.Db.AiMessages);
        Assert.Equal(AiRunStatus.Cancelled, (await fixture.Store.FindAsync(run.Id))!.Status);
    }

    [Fact]
    public async Task HeartbeatRace_ShouldRefreshRowVersionWithoutOverwritingCancellationOrLease()
    {
        await using var fixture = await Fixture.CreateAsync();
        var run = (await fixture.Store.ClaimAsync(fixture.RunId))!;
        var tracked = await fixture.Db.AiRuns.SingleAsync();
        fixture.Fence.Begin(run.Id, run.ExecutionLeaseId);
        tracked.ModelName = "updated";
        await fixture.Db.SaveChangesAsync();
        Assert.Equal("updated", (await fixture.Store.FindAsync(run.Id))!.ModelName);
        Assert.Equal(run.ExecutionLeaseId, tracked.ExecutionLeaseId);
        Assert.True(tracked.ProgressVersion > run.ProgressVersion);
    }

    [Fact]
    public async Task QueueDiscovery_ShouldRequireSystemScopeAndExecutionStayWithinTenant()
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.ListPendingAsync(10));
        fixture.Tenant.SetTenant(Guid.NewGuid(), "Request");
        Assert.Null(await fixture.Store.FindAsync(fixture.RunId));
        Assert.Null(await fixture.Store.ClaimAsync(fixture.RunId));
    }

    [Theory]
    [InlineData("stamp")]
    [InlineData("session")]
    [InlineData("inactive")]
    [InlineData("permission")]
    public async Task Identity_ShouldRejectRevocationAndStaleAuthority(string mutation)
    {
        await using var fixture = await Fixture.CreateAsync();
        var run = (await fixture.Store.FindAsync(fixture.RunId))!;
        var identities = new IdentityFake(run);
        var validator = new AiRunIdentityValidator(identities, identities, fixture.Config);
        Assert.NotNull(await validator.ValidateAsync(run));
        switch (mutation)
        {
            case "stamp": identities.Actor = identities.Actor! with { SecurityStamp = Guid.NewGuid() }; break;
            case "session": identities.SessionValid = false; break;
            case "inactive": identities.Actor = null; break;
            case "permission": identities.Actor = identities.Actor! with { PermissionCodes = [] }; break;
        }
        await Assert.ThrowsAsync<BusinessException>(() => validator.ValidateAsync(run));
    }

    [Fact]
    public async Task Submit_ShouldPersistContextAndReturnBeforeModelThenRecoverSameRunByKey()
    {
        await using var f = await Fixture.CreateAsync();
        var harness = await Harness.CreateAsync(f);
        var submitted = await harness.Service.SubmitAsync(harness.ConversationId, new() { Content = "synthetic query", UtcOffsetMinutes = 480 }, "stable-key");
        Assert.Equal(AiRunStatus.Pending, submitted.Status); Assert.Equal(0, harness.Gateway.Calls);
        var repeated = await harness.Service.SubmitAsync(harness.ConversationId, new() { Content = "synthetic query", UtcOffsetMinutes = 480 }, "stable-key");
        Assert.Equal(submitted.Id, repeated.Id);
        Assert.Equal(submitted.Id, (await harness.Service.GetSubmissionAsync(harness.ConversationId, "stable-key")).Id);
        Assert.Single(f.Db.AiMessages.Where(m => m.Role == AiMessageRole.User));
        Assert.Single(f.Db.AiMessages.Where(m => m.Role == AiMessageRole.Tool));
        await Assert.ThrowsAsync<BusinessException>(() => harness.Service.SubmitAsync(harness.ConversationId,
            new() { Content = "changed" }, "stable-key"));
        f.Db.ChangeTracker.Clear();
        var claimed = (await f.Store.ClaimAsync(submitted.Id))!;
        await harness.Service.ExecuteAsync(claimed.Id, claimed.ExecutionLeaseId);
        f.Db.ChangeTracker.Clear();
        var finished = await f.Store.FindAsync(claimed.Id);
        Assert.Equal(AiRunStatus.Completed, finished!.Status); Assert.Equal(1, harness.Gateway.Calls);
        Assert.Single(f.Db.AiMessages.Where(m => m.Role == AiMessageRole.Assistant));
        Assert.Single(f.Db.AiUsageLogs);
    }

    [Fact]
    public async Task CancelThroughConversationService_ShouldUseUntrackedOwnershipCheck()
    {
        await using var f = await Fixture.CreateAsync(); var harness = await Harness.CreateAsync(f);
        var pending = await harness.Service.SubmitAsync(harness.ConversationId, new() { Content = "query" }, "cancel-key");
        // An HTTP cancel uses a fresh scope after submission.
        f.Db.ChangeTracker.Clear();
        await harness.Service.CancelRunAsync(pending.Id);
        Assert.Equal(AiRunStatus.Cancelled, (await f.Store.FindAsync(pending.Id))!.Status);
        Assert.Equal(0, harness.Gateway.Calls);
    }

    [Fact]
    public async Task SubmissionAndWait_ShouldRejectStaleStampAndKeepRunWhenClientStopsWaiting()
    {
        await using var f = await Fixture.CreateAsync(); var harness = await Harness.CreateAsync(f);
        var originalActor = harness.Identities.Actor!;
        harness.Identities.Actor = originalActor with { SecurityStamp = Guid.NewGuid() };
        await Assert.ThrowsAsync<BusinessException>(() => harness.Service.SubmitAsync(harness.ConversationId,
            new() { Content = "query" }, "stale-key"));
        Assert.Empty(f.Db.AiMessages);
        harness.Identities.Actor = originalActor;
        var pending = await harness.Service.SubmitAsync(harness.ConversationId, new() { Content = "query" }, "wait-key");
        using var disconnected = new CancellationTokenSource(); disconnected.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Service.WaitAsync(pending.Id, disconnected.Token));
        Assert.Equal(AiRunStatus.Pending, (await f.Store.FindAsync(pending.Id))!.Status);
        f.Tenant.SetTenant(Guid.NewGuid(), "Request");
        await Assert.ThrowsAsync<BusinessException>(() => harness.Service.GetSubmissionAsync(harness.ConversationId, "wait-key"));
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("user")]
    [InlineData("stamp")]
    [InlineData("session")]
    [InlineData("permission")]
    public async Task ProgressReader_ShouldReturnMetadataOnlyAndRejectStaleOrForeignSubscriptions(string mutation)
    {
        await using var f = await Fixture.CreateAsync();
        var run = (await f.Store.FindAsync(f.RunId))!;
        var identities = new IdentityFake(run);
        identities.Actor = identities.Actor! with { PermissionCodes = [AiCenterConstants.ChatUsePermission, AiCenterConstants.ConversationViewPermission] };
        f.Db.AiToolInvocations.Add(new() { TenantId = run.TenantId, RunId = run.Id,
            InvocationId = "progress-1", ToolCode = "synthetic", Status = AiInvocationStatus.Running });
        await f.Db.SaveChangesAsync();
        var reader = new AiRunProgressReader(f.Store, identities, identities, f.Tenant, f.Config,
            new Repository<AiToolInvocation>(f.Db), new EfCoreAsyncQueryExecutor());
        Assert.Equal("progress-1", Assert.Single((await reader.ReadAsync(run.TenantId, run.ActorUserId,
            run.ActorSessionId!, run.ActorSecurityStamp!.Value, run.Id))!.Tools).InvocationId);
        var tenantId = run.TenantId; var userId = run.ActorUserId;
        switch (mutation)
        {
            case "tenant": tenantId = Guid.NewGuid(); break;
            case "user": userId = Guid.NewGuid(); break;
            case "stamp": identities.Actor = identities.Actor with { SecurityStamp = Guid.NewGuid() }; break;
            case "session": identities.SessionValid = false; break;
            case "permission": identities.Actor = identities.Actor with { PermissionCodes = [AiCenterConstants.ChatUsePermission] }; break;
        }
        Assert.Null(await reader.ReadAsync(tenantId, userId, run.ActorSessionId!, run.ActorSecurityStamp!.Value, run.Id));
    }

    [Fact]
    public async Task Background_ShouldNotProvideDraftActionsAndRejectSecondActiveSubmission()
    {
        await using var f = await Fixture.CreateAsync(); var harness = await Harness.CreateAsync(f);
        var pending = await harness.Service.SubmitAsync(harness.ConversationId, new() { Content = "prepare a draft" }, "first");
        await Assert.ThrowsAsync<BusinessException>(() => harness.Service.SubmitAsync(harness.ConversationId, new() { Content = "second" }, "second"));
        f.Db.ChangeTracker.Clear(); var claimed = (await f.Store.ClaimAsync(pending.Id))!;
        await harness.Service.ExecuteAsync(claimed.Id, claimed.ExecutionLeaseId);
        Assert.Empty(Assert.Single(harness.Gateway.Requests).Tools);
        Assert.Empty(f.Db.DemoBusinessOrders); Assert.Empty(f.Db.AiDocumentDrafts);
    }

    [Fact]
    public async Task RevokeDuringModel_ShouldNotCommitAnswerButKeepCostEvidence()
    {
        await using var f = await Fixture.CreateAsync(); var harness = await Harness.CreateAsync(f);
        var pending = await harness.Service.SubmitAsync(harness.ConversationId, new() { Content = "query" }, "first");
        f.Db.ChangeTracker.Clear(); var claimed = (await f.Store.ClaimAsync(pending.Id))!;
        harness.Gateway.OnRequest = () => harness.Identities.SessionValid = false;
        await harness.Service.ExecuteAsync(claimed.Id, claimed.ExecutionLeaseId);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(AiRunStatus.Failed, (await f.Store.FindAsync(pending.Id))!.Status);
        Assert.Empty(f.Db.AiMessages.Where(m => m.Role == AiMessageRole.Assistant));
        Assert.Single(f.Db.AiUsageLogs); Assert.Null((await f.Db.AiUsageLogs.SingleAsync()).ReservedCost);
        Assert.Equal(1, (await f.Db.AiUsageLogs.SingleAsync()).InputTokens);
        Assert.Equal(1, (await f.Db.AiUsageLogs.SingleAsync()).OutputTokens);
    }

    [Fact]
    public async Task BackgroundTimeout_ShouldNotCallFallbackWhenSupplierOutcomeIsUnknown()
    {
        await using var f = await Fixture.CreateAsync(); var harness = await Harness.CreateAsync(f);
        var pending = await harness.Service.SubmitAsync(harness.ConversationId, new() { Content = "query" }, "first");
        f.Db.ChangeTracker.Clear(); var claimed = (await f.Store.ClaimAsync(pending.Id))!;
        harness.Gateway.ThrowTimeout = true;
        await harness.Service.ExecuteAsync(claimed.Id, claimed.ExecutionLeaseId);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(1, harness.Gateway.Calls);
        Assert.Equal(AiRunStatus.Failed, (await f.Store.FindAsync(pending.Id))!.Status);
        Assert.Single(f.Db.AiUsageLogs);
    }

    private sealed class Harness
    {
        public AiConversationService Service { get; private init; } = null!;
        public Guid ConversationId { get; private init; }
        public IdentityFake Identities { get; private init; } = null!;
        public GatewayFake Gateway { get; private init; } = null!;
        public static async Task<Harness> CreateAsync(Fixture f)
        {
            var existing = (await f.Store.FindAsync(f.RunId))!;
            await f.Store.CancelAsync(existing.Id);
            var identities = new IdentityFake(existing);
            identities.Actor = identities.Actor! with { PermissionCodes = [AiCenterConstants.ChatUsePermission, AiCenterConstants.ConversationViewPermission] };
            var current = new AiRunExecutionIdentity(); current.Set(identities.Actor, existing.ActorSessionId!);
            var conversation = new AiConversation { TenantId = existing.TenantId, UserId = existing.ActorUserId,
                Title = "Synthetic", AgentCode = "permission-platform-agent", AgentVersion = "2.2",
                LastMessageAt = DateTimeOffset.UtcNow, RetentionUntil = DateTimeOffset.UtcNow.AddDays(30) };
            var provider = new AiProviderConfig { TenantId = existing.TenantId, ProviderCode = "synthetic", ProviderName = "Synthetic",
                BaseUrl = "https://evaluation.invalid", ModelName = "synthetic", ApiKeyEncrypted = "protected:synthetic",
                IsDefault = true, IsEnabled = true, SupportsTools = true, ComplianceConfirmedAt = DateTimeOffset.UtcNow,
                AllowedHostsJson = "[\"evaluation.invalid\"]" };
            f.Db.AiConversations.Add(conversation); f.Db.AiProviderConfigs.Add(provider); await f.Db.SaveChangesAsync();
            f.Db.ChangeTracker.Clear();
            IRepository<T> Repo<T>() where T : BaseEntity => new Repository<T>(f.Db);
            var gateway = new GatewayFake();
            var service = new AiConversationService(Repo<AiConversation>(), Repo<AiMessage>(), Repo<AiRun>(), Repo<AiProviderConfig>(),
                Repo<AiToolInvocation>(), Repo<AiUsageLog>(), new EfCoreAsyncQueryExecutor(), current,
                new EmptyTools(), gateway, new TestConfigValueProtector(), new AiRunCancellationProbe(f.Db),
                new AiRunCancellationCoordinator(), new NullAiRunRealtimeSender(), new MemoryUnit(f.Db), f.Config,
                executionStore: f.Store, identityValidator: new(identities, identities, f.Config), executionFence: f.Fence, identities: identities);
            return new() { Service = service, ConversationId = conversation.Id, Identities = identities, Gateway = gateway };
        }
    }

    private sealed class GatewayFake : IAiModelGateway
    {
        public int Calls { get; private set; }
        public bool ThrowTimeout { get; set; }
        public Action? OnRequest { get; set; }
        public List<AiModelGatewayRequest> Requests { get; } = [];
        public Task<AiModelGatewayResponse> CompleteAsync(AiProviderConnectionSettings provider, AiModelGatewayRequest request, CancellationToken token = default)
        {
            Calls++; Requests.Add(request); OnRequest?.Invoke();
            if (ThrowTimeout) throw new AiModelGatewayException("provider_timeout", ErrorCode.InternalServerError, "Synthetic timeout", true);
            return Task.FromResult(new AiModelGatewayResponse { Content = "synthetic", InputTokens = 1, OutputTokens = 1 });
        }
    }
    private sealed class EmptyTools : IAiReadOnlyToolRegistry
    {
        public IReadOnlyList<AiToolDefinition> GetAvailableTools() => [];
        public Task<AiToolExecutionResult> ExecuteAsync(string code, string args, CancellationToken token = default) => throw new NotSupportedException();
    }
    private sealed class MemoryUnit(AppDbContext db) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken token = default) => db.SaveChangesAsync(token);
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken token = default) => action(token);
    }

    private sealed class IdentityFake : IUserCredentialValidator, IUserSessionStatusChecker
    {
        public AuthenticatedUser? Actor { get; set; }
        public bool SessionValid { get; set; } = true;
        public IdentityFake(AiRun run) { Actor = new(run.ActorUserId, "synthetic", run.TenantId, null,
            run.ActorSecurityStamp!.Value, [], [AiCenterConstants.ChatUsePermission]); }
        public Task<AuthenticatedUser?> GetAuthenticationStateAsync(Guid tenantId, Guid userId, CancellationToken token = default) => Task.FromResult(Actor);
        public Task<Guid?> ResolveActiveTenantIdAsync(string code, CancellationToken token = default) => throw new NotSupportedException();
        public Task<AuthenticatedUser?> ValidateAsync(string username, string password, CancellationToken token = default) => throw new NotSupportedException();
        public Task<UserAccessValidationStatus> ValidateAccessAsync(Guid tenant, Guid user, string session, Guid stamp, CancellationToken token = default) =>
            Task.FromResult(SessionValid ? UserAccessValidationStatus.Valid : UserAccessValidationStatus.InvalidSession);
        public Task<bool> IsValidForRefreshAsync(Guid tenant, Guid user, string session, CancellationToken token = default) => Task.FromResult(SessionValid);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public AppDbContext Db { get; private set; } = null!;
        public TenantContext Tenant { get; private init; } = null!;
        public AiRunExecutionFence Fence { get; } = new();
        public IAiCenterConfiguration Config { get; private init; } = null!;
        public AiRunExecutionStore Store { get; private set; } = null!;
        public Guid RunId { get; } = Guid.NewGuid();
        public Guid OriginalLease { get; } = Guid.NewGuid();
        public static async Task<Fixture> CreateAsync()
        {
            var tenant = new TenantContext(); var tenantId = Guid.NewGuid(); tenant.SetTenant(tenantId, "Request");
            var config = new PermissionSystem.Infrastructure.Options.AiCenterOptions { Enabled = true, AllowedTenantIds = [tenantId] };
            var fixture = new Fixture { Tenant = tenant, Config = config };
            fixture.Db = new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
                tenant, new NullAuditContext(), fixture.Fence);
            fixture.Store = new(fixture.Db, tenant, config);
            fixture.Db.AiRuns.Add(new() { Id = fixture.RunId, TenantId = tenantId, ActorUserId = Guid.NewGuid(),
                ActorSessionId = "synthetic-session", ActorSecurityStamp = Guid.NewGuid(), ExecutionMode = "Background",
                Status = AiRunStatus.Pending, ConversationId = Guid.NewGuid(), ExecutionLeaseId = fixture.OriginalLease,
                QueueDeadlineAt = DateTimeOffset.UtcNow.AddMinutes(5), DeadlineAt = DateTimeOffset.UtcNow.AddSeconds(90), ProgressVersion = 1 });
            await fixture.Db.SaveChangesAsync(); fixture.Db.ChangeTracker.Clear(); return fixture;
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
