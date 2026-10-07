using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class Aic012ScenarioOperationsTests
{
    [Fact]
    public async Task Query_UsesTerminalDenominatorAndOwnedCurrentFeedback()
    {
        var f = new Aic012Fixture();
        var scenario = f.AddScenario();
        var positive = f.AddRun(AiRunStatus.Completed, scenario.Id, 100);
        var negative = f.AddRun(AiRunStatus.Completed, scenario.Id, 200);
        var noAnswer = f.AddRun(AiRunStatus.Completed, scenario.Id, -1); noAnswer.ResponseMessageId = null;
        var timeout = f.AddRun(AiRunStatus.Failed, scenario.Id, 500); timeout.ErrorCode = "run_timeout";
        f.AddRun(AiRunStatus.Failed, scenario.Id).ErrorCode = "run_orphaned";
        f.AddRun(AiRunStatus.Cancelled, scenario.Id, 0);
        f.AddRun(AiRunStatus.Pending, scenario.Id, 999);
        f.AddRun(AiRunStatus.Running, scenario.Id, 999);
        f.AddRun((AiRunStatus)99, scenario.Id, 999);
        f.AddFeedback(positive, AiFeedbackRating.Positive);
        f.AddFeedback(negative, AiFeedbackRating.Negative).ReasonCode = "incorrect";
        f.AddFeedback(negative, AiFeedbackRating.Positive).UserId = Guid.NewGuid();
        f.AddFeedback(positive, AiFeedbackRating.Negative).MessageId = Guid.NewGuid();
        var result = await f.Service().QueryAsync(f.Request);
        var row = Assert.Single(result.Scenarios.Items);
        Assert.Equal(9, row.RunCount); Assert.Equal(6, row.TerminalRunCount);
        Assert.Equal(3, row.CompletedRunCount); Assert.Equal(2, row.FailedRunCount);
        Assert.Equal(1, row.CancelledRunCount); Assert.Equal(1, row.PendingRunCount);
        Assert.Equal(1, row.RunningRunCount); Assert.Equal(1, row.UnknownStatusRunCount);
        Assert.Equal(1, row.TimeoutFailureCount); Assert.Equal(50m, row.TechnicalCompletionRate);
        Assert.Equal(2, row.FeedbackEligibleRunCount); Assert.Equal(1, row.PositiveFeedbackCount);
        Assert.Equal(1, row.NegativeFeedbackCount); Assert.Equal(100m, row.FeedbackCoverageRate);
        Assert.Equal(50m, row.PositiveFeedbackRate); Assert.Equal(4, row.DurationSampleCount);
        Assert.Equal(500, row.P95DurationMilliseconds); Assert.True(result.ObservedTo >= result.ObservedFrom);
    }

    [Theory]
    [InlineData("run_timeout", AiRunStatus.Failed, 1)]
    [InlineData("run_queue_timeout", AiRunStatus.Failed, 1)]
    [InlineData("run_timeout", AiRunStatus.Cancelled, 0)]
    [InlineData("run_interrupted", AiRunStatus.Failed, 0)]
    [InlineData("run_orphaned", AiRunStatus.Failed, 0)]
    public async Task Query_DoesNotGuessTimeouts(string code, AiRunStatus status, long expected)
    {
        var f = new Aic012Fixture(); f.AddRun(status).ErrorCode = code;
        Assert.Equal(expected, Assert.Single((await f.Service().QueryAsync(f.Request)).Scenarios.Items).TimeoutFailureCount);
    }

    [Fact]
    public async Task Query_SeparatesActualTokensEstimatedCostsUnknownAndUnsettled()
    {
        var f = new Aic012Fixture(); var run = f.AddRun(AiRunStatus.Completed);
        f.AddUsage(run, AiInvocationStatus.Completed, 10, 20, 2m, "USD");
        f.AddUsage(run, AiInvocationStatus.Failed, 4, 5, 1m, "USD");
        f.AddUsage(run, AiInvocationStatus.Completed, 0, 0, 0m, "CNY");
        var missing = f.AddUsage(run, AiInvocationStatus.Completed, null, 8, null, null);
        missing.EstimatedInputTokens = 900; missing.ReservedCost = 100;
        f.AddUsage(run, AiInvocationStatus.Cancelled, -10, 2, -1m, "usd");
        f.AddUsage(run, AiInvocationStatus.Running, 3, null, 50m, "USD");
        f.AddUsage(run, (AiInvocationStatus)99, 1, 1, 90m, "USD");
        var row = Assert.Single((await f.Service().QueryAsync(f.Request)).Scenarios.Items);
        Assert.Equal(18, row.InputTokens); Assert.Equal(36, row.OutputTokens);
        Assert.Equal(2, row.UnknownUsageInvocationCount); Assert.Equal(2, row.UnknownCostInvocationCount);
        Assert.Equal(1, row.UnsettledInvocationCount); Assert.Equal(1, row.UnknownStatusInvocationCount);
        Assert.Collection(row.EstimatedCosts,
            c => { Assert.Equal("CNY", c.Currency); Assert.Equal(0m, c.Amount); },
            c => { Assert.Equal("USD", c.Currency); Assert.Equal(3m, c.Amount); });
    }

    [Fact]
    public async Task Query_PreservesUnboundUnavailableDisabledAndRenamedScenarios()
    {
        var f = new Aic012Fixture(); var active = f.AddScenario(); var missing = Guid.NewGuid();
        var deleted = f.AddScenario(); deleted.IsDeleted = true;
        active.IsEnabled = false; active.Name = "当前名称";
        f.AddRun(AiRunStatus.Completed, active.Id); f.AddRun(AiRunStatus.Failed, active.Id);
        f.AddRun(AiRunStatus.Completed, null); f.AddRun(AiRunStatus.Completed, missing);
        f.AddRun(AiRunStatus.Completed, deleted.Id);
        f.AddScenario();
        var rows = (await f.Service().QueryAsync(f.Request)).Scenarios.Items;
        Assert.Equal(4, rows.Count); Assert.Equal(active.Id, rows[0].ScenarioId);
        Assert.Equal("当前名称", rows[0].ScenarioName); Assert.True(rows[0].ScenarioAvailable);
        Assert.Equal("未绑定场景", rows.Single(r => r.ScenarioId is null).ScenarioName);
        Assert.All(rows.Where(r => r.ScenarioId == missing || r.ScenarioId == deleted.Id),
            r => { Assert.Equal("场景不可用", r.ScenarioName); Assert.False(r.ScenarioAvailable); Assert.Null(r.ScenarioCode); });
    }

    [Fact]
    public async Task Query_PaginatesGroupsWithoutChangingTheirPopulation()
    {
        var f = new Aic012Fixture(); var a = f.AddScenario(); var b = f.AddScenario();
        f.AddRun(AiRunStatus.Completed, a.Id); f.AddRun(AiRunStatus.Failed, a.Id);
        f.AddRun(AiRunStatus.Completed, b.Id);
        var row = await f.Service().QueryAsync(new() { From = f.From, To = f.To, PageIndex = 2, PageSize = 1 });
        Assert.Equal(2, row.Scenarios.TotalCount); Assert.Equal(2, row.Scenarios.PageIndex);
        Assert.Equal(b.Id, Assert.Single(row.Scenarios.Items).ScenarioId);
        Assert.Empty((await f.Service().QueryAsync(new() { From = f.From, To = f.To, PageIndex = 3, PageSize = 1 })).Scenarios.Items);
    }

    [Fact]
    public async Task Query_ReturnsNullForUnavailableSamplesAndExcludesDeletedAndOtherTenants()
    {
        var f = new Aic012Fixture(); f.AddRun(AiRunStatus.Pending);
        f.AddRun(AiRunStatus.Completed).IsDeleted = true;
        var other = f.AddRun(AiRunStatus.Completed); other.TenantId = Guid.NewGuid();
        var row = Assert.Single((await f.Service().QueryAsync(f.Request)).Scenarios.Items);
        Assert.Equal(1, row.RunCount); Assert.Null(row.TechnicalCompletionRate);
        Assert.Null(row.FeedbackCoverageRate); Assert.Null(row.PositiveFeedbackRate); Assert.Null(row.P95DurationMilliseconds);
        Assert.Empty(row.EstimatedCosts);
        var empty = new Aic012Fixture(); Assert.Empty((await empty.Service().QueryAsync(empty.Request)).Scenarios.Items);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(20, 19)]
    [InlineData(21, 20)]
    public async Task Query_UsesExactNearestRankP95(int count, long expected)
    {
        var f = new Aic012Fixture();
        for (var index = count; index >= 1; index--) f.AddRun(AiRunStatus.Completed, duration: index);
        var row = Assert.Single((await f.Service().QueryAsync(f.Request)).Scenarios.Items);
        Assert.Equal(expected, row.P95DurationMilliseconds); Assert.Equal(count, row.DurationSampleCount);
    }

    [Fact]
    public async Task Query_UsesCreatedAtHalfOpenIntervalAndCurrentFeedback()
    {
        var f = new Aic012Fixture(); var start = f.AddRun(AiRunStatus.Completed); start.CreatedAt = f.From;
        f.AddFeedback(start, AiFeedbackRating.Positive).CreatedAt = f.To.AddDays(1);
        f.AddRun(AiRunStatus.Completed).CreatedAt = f.To;
        f.AddRun(AiRunStatus.Completed).CreatedAt = f.From.AddTicks(-1);
        var row = Assert.Single((await f.Service().QueryAsync(f.Request)).Scenarios.Items);
        Assert.Equal(1, row.RunCount); Assert.Equal(1, row.PositiveFeedbackCount);
        var defaults = await f.Service().QueryAsync(new()); Assert.Equal(TimeSpan.FromDays(30), defaults.To - defaults.From);
        await f.Service().QueryAsync(new() { From = f.To.AddDays(-90), To = f.To });
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    [InlineData(int.MaxValue, 20)]
    public async Task Query_RejectsInvalidPagination(int page, int size)
    {
        var f = new Aic012Fixture();
        var error = await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryAsync(new() { PageIndex = page, PageSize = size }));
        Assert.Equal(ErrorCode.ValidationFailed, error.ErrorCode); Assert.Equal(0, f.Queries.ExecutionCount);
    }

    [Theory]
    [InlineData("reversed")]
    [InlineData("long")]
    [InlineData("future")]
    [InlineData("underflow")]
    public async Task Query_RejectsInvalidDatesWithoutQueries(string kind)
    {
        var f = new Aic012Fixture();
        var request = kind switch
        {
            "reversed" => new AiScenarioOperationsQueryRequest { From = f.To, To = f.From },
            "long" => new() { From = f.To.AddDays(-91), To = f.To },
            "future" => new() { To = DateTimeOffset.UtcNow.AddMinutes(6) },
            _ => new() { To = DateTimeOffset.MinValue }
        };
        Assert.Equal(ErrorCode.ValidationFailed, (await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryAsync(request))).ErrorCode);
        Assert.Equal(0, f.Queries.ExecutionCount);
    }

    [Theory]
    [InlineData("anonymous", ErrorCode.Unauthorized)]
    [InlineData("no-claim", ErrorCode.Forbidden)]
    [InlineData("revoked", ErrorCode.Forbidden)]
    [InlineData("inactive", ErrorCode.Unauthorized)]
    [InlineData("no-target", ErrorCode.Forbidden)]
    [InlineData("other-target", ErrorCode.Forbidden)]
    [InlineData("system", ErrorCode.Forbidden)]
    [InlineData("inactive-target", ErrorCode.Forbidden)]
    [InlineData("stale-super", ErrorCode.Forbidden)]
    public async Task Query_RejectsInvalidAccessBeforeReading(string condition, ErrorCode expected)
    {
        var f = new Aic012Fixture();
        switch (condition)
        {
            case "anonymous": f.Current.UserId = null; break;
            case "no-claim": f.Current = new(); break;
            case "revoked": f.Identities.Actor = f.Identities.Actor! with { PermissionCodes = [] }; break;
            case "inactive": f.Identities.Actor = null; break;
            case "no-target": f.Tenant.TenantId = null; break;
            case "other-target": f.Tenant.TenantId = Guid.NewGuid(); break;
            case "system": f.Tenant.IsSystemScopeActive = true; break;
            case "inactive-target": f.Identities.ActiveTenants.Clear(); break;
            case "stale-super": f.Current.IsSuperAdmin = true; break;
        }
        Assert.Equal(expected, (await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryAsync(f.Request))).ErrorCode);
        Assert.Equal(0, f.Queries.ExecutionCount);
    }

    [Fact]
    public async Task Query_SuperAdminReadsOnlyExplicitActiveTargetWithFreshRole()
    {
        var f = new Aic012Fixture(); var target = Guid.NewGuid();
        f.Current.IsSuperAdmin = true; f.Identities.Actor = f.Identities.Actor! with { Roles = [ClaimConstants.SuperAdminRoleCode] };
        f.Tenant.TenantId = target; f.Identities.ActiveTenants.Add(target);
        f.AddRun(AiRunStatus.Completed);
        f.AddRun(AiRunStatus.Failed).TenantId = target;
        var row = Assert.Single((await f.Service().QueryAsync(f.Request)).Scenarios.Items);
        Assert.Equal(1, row.RunCount); Assert.Equal(1, row.FailedRunCount); Assert.Equal(0, row.CompletedRunCount);
    }

    [Fact]
    public async Task Query_DoesNotTrustCrossTenantUsageFeedbackOrScenarioMetadata()
    {
        var f = new Aic012Fixture(); var scene = f.AddScenario(); scene.TenantId = Guid.NewGuid();
        var run = f.AddRun(AiRunStatus.Completed, scene.Id);
        f.AddUsage(run, AiInvocationStatus.Completed, 10, 10, 3, "USD").TenantId = scene.TenantId;
        f.AddFeedback(run, AiFeedbackRating.Positive).TenantId = scene.TenantId;
        var row = Assert.Single((await f.Service().QueryAsync(f.Request)).Scenarios.Items);
        Assert.Equal("场景不可用", row.ScenarioName); Assert.Equal(0, row.PositiveFeedbackCount);
        Assert.Equal(0, row.InputTokens); Assert.Empty(row.EstimatedCosts);
    }

    [Theory]
    [InlineData("revoked", ErrorCode.Forbidden)]
    [InlineData("stamp", ErrorCode.Forbidden)]
    [InlineData("deleted", ErrorCode.Conflict)]
    [InlineData("moved", ErrorCode.Conflict)]
    [InlineData("scenario-deleted", ErrorCode.Conflict)]
    public async Task Query_RejectsChangesBeforeReturningEvidence(string change, ErrorCode expected)
    {
        var f = new Aic012Fixture(); var scene = f.AddScenario(); var run = f.AddRun(AiRunStatus.Completed, scene.Id);
        f.Queries.AfterList = count =>
        {
            if (count != (change == "scenario-deleted" ? 2 : 1)) return;
            switch (change)
            {
                case "revoked": f.Identities.Actor = f.Identities.Actor! with { PermissionCodes = [] }; break;
                case "stamp": f.Identities.Actor = f.Identities.Actor! with { SecurityStamp = Guid.NewGuid() }; break;
                case "deleted": run.IsDeleted = true; break;
                case "moved": run.TenantId = Guid.NewGuid(); break;
                case "scenario-deleted": scene.IsDeleted = true; break;
            }
        };
        // Usage is also queried for a nonempty population; scenario metadata is the fourth list read.
        if (change == "scenario-deleted") f.Queries.AfterList = count => { if (count == 4) scene.IsDeleted = true; };
        Assert.Equal(expected, (await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryAsync(f.Request))).ErrorCode);
    }

    [Theory]
    [InlineData("run")]
    [InlineData("usage")]
    [InlineData("feedback")]
    public async Task Query_RejectsCapacityInsteadOfReturningTruncatedStatistics(string kind)
    {
        var f = new Aic012Fixture(); var run = f.AddRun(AiRunStatus.Completed);
        var limit = kind == "run" ? AiScenarioOperationsContract.MaxRuns :
            kind == "usage" ? AiScenarioOperationsContract.MaxUsages : AiScenarioOperationsContract.MaxFeedback;
        for (var index = kind == "run" ? 1 : 0; index <= limit; index++)
        {
            if (kind == "run") f.AddRun(AiRunStatus.Completed);
            else if (kind == "usage") f.AddUsage(run, AiInvocationStatus.Completed, 0, 0, 0, "USD");
            else f.AddFeedback(run, AiFeedbackRating.Positive);
        }
        var error = await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryAsync(f.Request));
        Assert.Equal(ErrorCode.ValidationFailed, error.ErrorCode); Assert.Equal(AiScenarioOperationsContract.CapacityMessage, error.Message);
        Assert.Contains(limit + 1, f.Queries.MaterializedCounts);
    }

    [Fact]
    public async Task Query_DoesNotReturnSensitiveFactsOrFabricatedBusinessMetrics()
    {
        var f = new Aic012Fixture(); var run = f.AddRun(AiRunStatus.Completed);
        run.ErrorSummary = run.ExecutionConfigurationJson = "DO_NOT_EXPORT";
        f.AddFeedback(run, AiFeedbackRating.Negative).Comment = "DO_NOT_EXPORT";
        var result = await f.Service().QueryAsync(f.Request);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("DO_NOT_EXPORT", json); Assert.DoesNotContain("ActorUserId", json);
        Assert.DoesNotContain("ResponseMessageId", json); Assert.DoesNotContain("HumanCorrectionRate", json);
        Assert.DoesNotContain(run.Id.ToString(), json);
    }

    [Fact]
    public async Task Query_CancellationIsPropagated()
    {
        var f = new Aic012Fixture(); using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service().QueryAsync(f.Request, source.Token));
        Assert.Equal(0, f.Queries.ExecutionCount);
    }

    [Fact]
    public async Task Query_KeepsObservedEligibilityWhenARunCompletesDuringLaterReads()
    {
        var f = new Aic012Fixture(); var run = f.AddRun(AiRunStatus.Pending);
        f.Queries.AfterList = count =>
        {
            if (count != 1) return;
            run.Status = AiRunStatus.Completed; run.ResponseMessageId = Guid.NewGuid();
            f.AddFeedback(run, AiFeedbackRating.Positive);
        };
        var row = Assert.Single((await f.Service().QueryAsync(f.Request)).Scenarios.Items);
        Assert.Equal(1, row.PendingRunCount); Assert.Equal(0, row.FeedbackEligibleRunCount);
        Assert.Equal(0, row.PositiveFeedbackCount); Assert.Null(row.FeedbackCoverageRate);
    }

    [Fact]
    public async Task Query_RoundsPercentagesAndTreatsRetryRunsAsSeparateAttempts()
    {
        var f = new Aic012Fixture(); var original = f.AddRun(AiRunStatus.Failed);
        f.AddRun(AiRunStatus.Completed).RetryOfRunId = original.Id;
        f.AddRun(AiRunStatus.Cancelled);
        var row = Assert.Single((await f.Service().QueryAsync(f.Request)).Scenarios.Items);
        Assert.Equal(3, row.RunCount); Assert.Equal(33.33m, row.TechnicalCompletionRate);
    }
}

internal sealed class Aic012Fixture
{
    public DateTimeOffset To { get; } = new(DateTime.UtcNow.Date, TimeSpan.Zero);
    public DateTimeOffset From => To.AddDays(-1);
    public AiScenarioOperationsQueryRequest Request => new() { From = From, To = To };
    public TestCurrentUserService Current { get; set; } = new(permissions: [AiCenterConstants.OperationsViewPermission]);
    public TestTenant Tenant { get; } = new();
    public IdentitySource Identities { get; } = new();
    public InMemoryRepository<AiRun> Runs { get; } = new();
    public InMemoryRepository<AiUsageLog> Usages { get; } = new();
    public InMemoryRepository<AiUserFeedback> Feedback { get; } = new();
    public InMemoryRepository<AiScenario> Scenarios { get; } = new();
    public RecordingExecutor Queries { get; } = new();
    public AiScenarioOperationsService Service() => new(Runs, Usages, Feedback, Scenarios, Queries, Current, Tenant, Identities);
    public AiRun AddRun(AiRunStatus status, Guid? scenario = null, long? duration = null)
    {
        var run = new AiRun { Id = Guid.NewGuid(), TenantId = TestIds.TenantId, CreatedAt = From.AddHours(1),
            ActorUserId = Current.UserId!.Value, Status = status, ScenarioId = scenario,
            ResponseMessageId = status == AiRunStatus.Completed ? Guid.NewGuid() : null, DurationMilliseconds = duration };
        Runs.AddAsync(run).GetAwaiter().GetResult(); return run;
    }
    public AiScenario AddScenario()
    {
        var scene = new AiScenario { Id = Guid.NewGuid(), TenantId = TestIds.TenantId, Name = "合成场景", Code = "synthetic" };
        Scenarios.AddAsync(scene).GetAwaiter().GetResult(); return scene;
    }
    public AiUserFeedback AddFeedback(AiRun run, AiFeedbackRating rating)
    {
        var entry = new AiUserFeedback { Id = Guid.NewGuid(), TenantId = run.TenantId, RunId = run.Id,
            UserId = run.ActorUserId, MessageId = run.ResponseMessageId!.Value, Rating = rating };
        Feedback.AddAsync(entry).GetAwaiter().GetResult(); return entry;
    }
    public AiUsageLog AddUsage(AiRun run, AiInvocationStatus status, int? input, int? output, decimal? cost, string? currency)
    {
        var entry = new AiUsageLog { Id = Guid.NewGuid(), TenantId = run.TenantId, RunId = run.Id,
            Status = status, InputTokens = input, OutputTokens = output, EstimatedCost = cost, PricingCurrency = currency };
        Usages.AddAsync(entry).GetAwaiter().GetResult(); return entry;
    }
    internal sealed class TestTenant : ITenantContext
    {
        public Guid? TenantId { get; set; } = TestIds.TenantId;
        public string? Source => "Synthetic test";
        public bool IsResolved => TenantId.HasValue;
        public bool IsSuperAdmin { get; private set; }
        public bool IsSystemScopeActive { get; set; }
        public bool IsHttpRequest => true;
        public void SetTenant(Guid tenantId, string source) => TenantId = tenantId;
        public void MarkAsSuperAdmin(bool isSuperAdmin) => IsSuperAdmin = isSuperAdmin;
        public void MarkAsHttpRequest() { }
    }
    internal sealed class IdentitySource : IUserCredentialValidator
    {
        public AuthenticatedUser? Actor { get; set; } = new(TestIds.NormalUserId, "Synthetic actor", TestIds.TenantId, null,
            Guid.NewGuid(), [], [AiCenterConstants.OperationsViewPermission]);
        public HashSet<Guid> ActiveTenants { get; } = [TestIds.TenantId];
        public Task<AuthenticatedUser?> GetAuthenticationStateAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); return Task.FromResult(Actor); }
        public Task<Guid?> ResolveActiveTenantIdAsync(string value, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); return Task.FromResult<Guid?>(Guid.TryParse(value, out var id) && ActiveTenants.Contains(id) ? id : null); }
        public Task<AuthenticatedUser?> ValidateAsync(string username, string password, CancellationToken ct = default) => throw new NotSupportedException();
    }
    internal sealed class RecordingExecutor : IAsyncQueryExecutor
    {
        private readonly InMemoryAsyncQueryExecutor _inner = new();
        public int ExecutionCount => _inner.ExecutionCount;
        public IReadOnlyList<int> MaterializedCounts => _inner.MaterializedItemCounts;
        public Action<int>? AfterList { get; set; }
        public async Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken ct = default)
        { var result = await _inner.ToListAsync(query, ct); AfterList?.Invoke(_inner.ListExecutionCount); return result; }
        public Task<long> LongCountAsync<T>(IQueryable<T> query, CancellationToken ct = default) => _inner.LongCountAsync(query, ct);
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct = default) => _inner.AnyAsync(query, ct);
        public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct = default) => _inner.FirstOrDefaultAsync(query, ct);
    }
}
