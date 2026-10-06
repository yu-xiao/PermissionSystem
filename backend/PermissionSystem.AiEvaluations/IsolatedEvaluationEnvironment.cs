using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Departments;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Application.Users;
using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Infrastructure.Authentication;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Queries;
using PermissionSystem.Infrastructure.RateLimiting;
using PermissionSystem.Infrastructure.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.AiEvaluations;

internal sealed class IsolatedEvaluationEnvironment : IAsyncDisposable
{
    public static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid OtherTenantId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid ActorId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid TargetId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    public static readonly Guid ParentMenuId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    public static readonly Guid ChildMenuId = Guid.Parse("40000000-0000-0000-0000-000000000002");
    private static readonly Guid DepartmentId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private readonly string _databaseName = Guid.NewGuid().ToString("N");
    private readonly InMemoryDatabaseRoot _databaseRoot = new();
    private readonly TenantContext _tenant = new();
    private readonly FixtureSettings _settings;
    private readonly ScriptedGateway? _script;
    private readonly LocalLock _localLock = new();
    private readonly EvaluationIdentity _current = new();
    private readonly EvaluationConfiguration _configuration = new();
    private IAiReadOnlyToolHandler[] _handlers = [];
    private AiConversationService _service = null!;
    private AiStructuredResultReader _reader = null!;
    private RecordingGateway _gateway = null!;
    private RecordingToolRegistry _tools = null!;
    private AiConversationDetailResponse _conversation = null!;
    private AppDbContext _db = null!;
    private AiContextReference? _firstReference;

    private IsolatedEvaluationEnvironment(FixtureSettings settings, ScriptedGateway? script)
    {
        _settings = settings;
        _script = script;
    }

    public static async Task<IsolatedEvaluationEnvironment> CreateAsync(FixtureSettings settings,
        EvaluationBudget budget, IAiModelGateway? liveGateway = null, LiveEvaluationSettings? live = null,
        string? apiKey = null, CancellationToken cancellationToken = default, AiScenarioSnapshot? snapshot = null)
    {
        var environment = new IsolatedEvaluationEnvironment(settings, liveGateway is null ? new ScriptedGateway() : null);
        try
        {
            await environment.InitializeAsync(budget, liveGateway, live, apiKey, cancellationToken, snapshot);
            return environment;
        }
        catch { await environment.DisposeAsync(); throw; }
    }

    private async Task InitializeAsync(EvaluationBudget budget, IAiModelGateway? liveGateway,
        LiveEvaluationSettings? live, string? apiKey, CancellationToken cancellationToken, AiScenarioSnapshot? snapshot)
    {
        if (_settings.ActorScope is not ("All" or "CurrentUser")) throw new EvaluationInputException("Unsupported fixture scope.");
        _tenant.SetTenant(TenantId, "evaluation");
        _db = new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_databaseName, _databaseRoot).Options, _tenant, new NullAuditContext());
        var actorRole = Add(new Role { Code = "EvaluationOperator", Name = "合成操作员" });
        var targetTenant = _settings.CrossTenantTarget ? OtherTenantId : TenantId;
        var targetRole = Add(new Role { Code = "EvaluationTarget", Name = "合成目标角色", TenantId = targetTenant });
        Add(new Tenant { Id = TenantId, TenantId = TenantId, Code = "evaluation", Name = "合成租户", Status = _settings.TenantActive ? TenantStatus.Active : TenantStatus.Disabled });
        Add(new Tenant { Id = OtherTenantId, TenantId = OtherTenantId, Code = "evaluation-other", Name = "合成其他租户" });
        Add(new Department { Id = DepartmentId, Code = "evaluation", Name = "合成部门", TreePath = $"/{DepartmentId}/" });
        Add(new User { Id = ActorId, UserName = "eval-actor", NormalizedUserName = "EVAL-ACTOR", DisplayName = "同名合成用户", DepartmentId = DepartmentId, IsEnabled = _settings.ActorEnabled });
        Add(new User { Id = TargetId, UserName = "eval-target", NormalizedUserName = "EVAL-TARGET", DisplayName = _settings.CrossTenantTarget ? "UNREADABLE-CANARY" : "同名合成用户", TenantId = targetTenant, DepartmentId = _settings.CrossTenantTarget ? null : DepartmentId });
        Add(new UserRole { UserId = ActorId, RoleId = actorRole.Id });
        Add(new UserRole { UserId = TargetId, RoleId = targetRole.Id, TenantId = targetTenant });
        var actorPermissions = new List<string>
        {
            AiCenterConstants.ChatUsePermission, AiCenterConstants.ConversationViewPermission, AiCenterConstants.ToolQueryPermission,
            AiCenterConstants.UserQueryPermission, "system:user:view", "system:role:view", "system:permission:view", "system:menu:view", "system:role:data-scope",
            AiCenterConstants.DepartmentQueryPermission, "system:department:view", AiCenterConstants.RoleQueryPermission,
            AiCenterConstants.LoginLogQueryPermission, "system:login-log:view", AiCenterConstants.OperationLogQueryPermission, "system:operation-log:view", "business:view"
        };
        if (_settings.RemovePermission is not null && !actorPermissions.Remove(_settings.RemovePermission))
            throw new EvaluationInputException("Unknown fixture permission to remove.");
        if (_settings.Wildcard) actorPermissions.Add("*");
        foreach (var code in actorPermissions)
        {
            var permission = Add(new Permission { Code = code, Name = code, Group = "Evaluation" });
            Add(new RolePermission { RoleId = actorRole.Id, PermissionId = permission.Id });
        }
        var targetPermission = _settings.CrossTenantTarget
            ? Add(new Permission { Code = "business:view", Name = "合成目标权限", Group = "Evaluation", TenantId = targetTenant })
            : _db.ChangeTracker.Entries<Permission>().Single(p => p.Entity.Code == "business:view").Entity;
        Add(new RolePermission { RoleId = targetRole.Id, PermissionId = targetPermission.Id, TenantId = targetTenant });
        Add(new RoleDataScope { RoleId = actorRole.Id, ScopeType = Enum.Parse<DataScopeType>(_settings.ActorScope) });
        Add(new RoleDataScope { RoleId = targetRole.Id, ScopeType = DataScopeType.CurrentDepartment, TenantId = targetTenant });
        Add(new Menu { Id = ParentMenuId, Name = "合成父菜单", Visible = _settings.ParentVisible });
        Add(new Menu { Id = ChildMenuId, Name = "合成子菜单", ParentId = ParentMenuId });
        Add(new RoleMenu { RoleId = actorRole.Id, MenuId = ChildMenuId });
        if (!_settings.CrossTenantTarget) Add(new RoleMenu { RoleId = targetRole.Id, MenuId = ChildMenuId });
        Add(new AiProviderConfig
        {
            ProviderCode = live?.ProviderAlias ?? "offline", ProviderName = "Isolated evaluation",
            BaseUrl = live?.BaseUrl ?? "https://evaluation.invalid", ModelName = live?.Model ?? "scripted",
            ChatCompletionsPath = live?.ChatCompletionsPath ?? "v1/chat/completions", ApiKeyEncrypted = "evaluation-process-key",
            IsDefault = true, IsEnabled = true, ComplianceConfirmedAt = live?.ComplianceConfirmedAt ?? DateTimeOffset.Parse("2026-10-06T00:00:00Z"),
            AllowedHostsJson = System.Text.Json.JsonSerializer.Serialize(live?.AllowedHosts ?? ["evaluation.invalid"]),
            AllowPrivateNetwork = live?.AllowPrivateNetwork ?? false, TimeoutSeconds = live?.TimeoutSeconds ?? 30,
            Temperature = snapshot?.Configuration.Temperature ?? live?.Temperature ?? 0, MaxTokens = snapshot?.Configuration.MaxTokens ?? live?.MaxTokens ?? 2048,
            InputTokenPricePerMillion = live?.InputPricePerMillion ?? 1, OutputTokenPricePerMillion = live?.OutputPricePerMillion ?? 1,
            PricingCurrency = live?.Currency ?? "XXX"
        });
        using (new SystemTenantScope(_tenant, NullLogger<SystemTenantScope>.Instance).Begin("SyntheticEvaluationSetup"))
            await _db.SaveChangesAsync(cancellationToken);
        if (_settings.TargetDeleted)
        {
            _db.Users.Single(u => u.Id == TargetId).IsDeleted = true;
            await _db.SaveChangesAsync(cancellationToken);
        }
        _current.SetPermissions(actorPermissions);
        var unit = new IsolatedUnitOfWork(_db);
        var queries = new EfCoreAsyncQueryExecutor();
        var scopes = new DataScopeService(Repo<Role>(), Repo<User>(), Repo<UserRole>(), Repo<RoleDataScope>(), Repo<UserDataScope>(), Repo<Department>(), _current, NullLogger<DataScopeService>.Instance, unit);
        var menus = new CurrentUserAppService(_current, Repo<Menu>(), Repo<Role>(), Repo<RoleMenu>(), Repo<UserRole>(), Repo<RolePermission>(), Repo<Permission>(), _tenant);
        var identities = new UserCredentialValidator(_db, new PasswordHasher<User>(), _tenant);
        var filter = new DataPermissionFilter();
        var diagnostics = new PermissionDiagnosticService(_current, _tenant, identities, Repo<User>(), Repo<Menu>(), Repo<Role>(), Repo<UserRole>(), Repo<RolePermission>(), Repo<RoleMenu>(), Repo<Permission>(), scopes, filter, menus, queries, _configuration);
        var guard = new AiQueryAccessGuard(_current, _tenant, identities, _configuration, scopes);
        _reader = new(Repo<AiMessage>(), Repo<AiRun>(), Repo<AiConversation>(), Repo<AiToolInvocation>(), Repo<User>(), Repo<Department>(), _current, _tenant, queries, diagnostics, guard, filter, _configuration, Repo<Menu>());
        IAiReadOnlyToolHandler[] handlers = [
            new PermissionDiagnosticAiToolHandler(diagnostics, _current, _tenant),
            new UserSearchAiToolHandler(scopes, filter, Repo<User>(), queries, _configuration, guard, Repo<Department>()),
            new DepartmentSearchAiToolHandler(new DepartmentService(Repo<Department>(), Repo<User>(), new IsolatedTenantResolver(), unit), _configuration),
            new RoleSummaryAiToolHandler(Repo<Role>(), queries, _configuration),
            new LoginLogSummaryAiToolHandler(Repo<LoginLog>(), queries),
            new OperationLogSummaryAiToolHandler(Repo<OperationLog>(), queries)
        ];
        _handlers = handlers;
        var registry = new AiReadOnlyToolRegistry(handlers, _current, _tenant, new TraceContextAccessor());
        _tools = new(registry);
        _gateway = new(liveGateway ?? _script!, budget, registry.GetAvailableTools);
        var admission = new AiRunAdmissionService(Repo<AiRun>(), Repo<AiUsageLog>(), queries, new MemoryDistributedRateLimitService(), _localLock, _configuration);
        var budgetService = new AiBudgetService(Repo<AiBudgetPolicy>(), Repo<AiUsageLog>(), Repo<AiRun>(), Repo<User>(), queries, new IsolatedTenantResolver(), _localLock, unit);
        var alerts = new AiAlertService(Repo<User>(), Repo<UserRole>(), Repo<Role>(), Repo<Notification>(), Repo<UserNotification>(), queries, unit);
        CandidateEvaluationRuntime? runtime = null;
        if (snapshot is not null)
        {
            new AiScenarioSnapshotFactory(handlers, new PermissionSystem.Infrastructure.Ai.AiBuildIdentity()).Validate(snapshot);
            runtime = new(snapshot, _current, identities);
        }
        _service = new(Repo<AiConversation>(), Repo<AiMessage>(), Repo<AiRun>(), Repo<AiProviderConfig>(), Repo<AiToolInvocation>(), Repo<AiUsageLog>(), queries,
            _current, _tools, _gateway, new ProcessKeyProtector(apiKey ?? "offline-synthetic-key"), new IsolatedCancellationProbe(), new AiRunCancellationCoordinator(),
            new NullAiRunRealtimeSender(), unit, _configuration, budgetService: budgetService, admissionService: admission,
            circuitBreaker: new PermissionSystem.Infrastructure.Ai.AiCircuitBreaker(_localLock, alerts),
            structuredReader: _reader, followUp: new AiFollowUpContextService(_reader, guard),
            scenarioRuntime: runtime, buildIdentity: new PermissionSystem.Infrastructure.Ai.AiBuildIdentity());
        _conversation = await _service.CreateAsync(new() { Title = "AIC-004 synthetic evaluation", ScenarioId = runtime?.ScenarioId }, cancellationToken);
    }

    public AiScenarioSnapshot CandidateSnapshot(AiScenarioConfiguration? configuration = null) =>
        new AiScenarioSnapshotFactory(_handlers, new PermissionSystem.Infrastructure.Ai.AiBuildIdentity())
            .Create(TenantId, 1, configuration ?? new());

    public async Task<StepObservation> ExecuteAsync(EvaluationStep step, CancellationToken cancellationToken)
    {
        _gateway.Observations.Clear();
        _tools.Observations.Clear();
        await MutateAsync(step.Mutation, cancellationToken);
        _script?.Prepare(step);
        var observation = new StepObservation { Input = step.Input };
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = await _service.SendMessageAsync(_conversation.Id, new()
            {
                Content = step.Input, ContextRef = step.SelectFirstResult ? _firstReference ?? throw new EvaluationInputException("Missing first result for follow-up.") : null,
                UtcOffsetMinutes = step.UtcOffsetMinutes
            }, cancellationToken);
            observation.Status = result.Status.ToString();
            observation.ErrorCode = result.ErrorCode;
            observation.Output = result.ResponseMessage?.Content ?? "";
            observation.Evidence = result.StructuredResults.ToList();
            var first = result.StructuredResults.FirstOrDefault();
            if (first is not null) _firstReference ??= new(first.RunId, first.InvocationId);
            var run = await _db.AiRuns.FirstAsync(r => r.Id == result.Id, CancellationToken.None);
            observation.AgentVersion = run.AgentVersion;
            observation.PromptVersion = run.PromptVersion;
            observation.Usage = await _db.AiUsageLogs.Where(u => u.RunId == result.Id).OrderBy(u => u.Sequence)
                .Select(u => new UsageObservation(u.Status.ToString(), u.InputTokens, u.OutputTokens, u.EstimatedInputTokens,
                    u.EstimatedOutputTokens, u.EstimatedCost, u.PricingCurrency)).ToListAsync(CancellationToken.None);
        }
        catch (BusinessException exception) { observation.Status = "Rejected"; observation.ErrorCode = exception.ErrorCode.ToString(); }
        catch (OperationCanceledException) { observation.Status = "Cancelled"; observation.ErrorCode = "evaluation_cancelled"; }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        { observation.Status = "Failed"; observation.ErrorCode = "evaluation_execution_failed"; }
        observation.Models = _gateway.Observations.ToList();
        observation.Tools = _tools.Observations.ToList();
        observation.DurationMilliseconds = watch.ElapsedMilliseconds;
        return observation;
    }

    private async Task MutateAsync(FixtureMutation mutation, CancellationToken cancellationToken)
    {
        if (mutation == FixtureMutation.RevokeRoleView)
        {
            var permission = _db.Permissions.Single(p => p.Code == "system:role:view");
            _db.RolePermissions.Single(r => r.PermissionId == permission.Id).IsDeleted = true;
            await _db.SaveChangesAsync(cancellationToken);
        }
        else if (mutation == FixtureMutation.ExpireSource)
        {
            // Advance the age of synthetic history only; the production reader still checks its real retention cutoff.
            await using var aging = new DbContext(new DbContextOptionsBuilder<DbContext>()
                .UseInMemoryDatabase(_databaseName, _databaseRoot).UseModel(_db.Model).Options);
            var messages = await aging.Set<AiMessage>().IgnoreQueryFilters().Where(m => m.Role == AiMessageRole.Tool).ToListAsync(cancellationToken);
            foreach (var message in messages) message.CreatedAt = DateTimeOffset.UtcNow.AddDays(-31);
            await aging.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();
        }
    }

    private T Add<T>(T entity) where T : BaseEntity
    {
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (entity.TenantId == Guid.Empty) entity.TenantId = TenantId;
        _db.Add(entity);
        return entity;
    }
    private Repository<T> Repo<T>() where T : BaseEntity => new(_db);
    public async ValueTask DisposeAsync()
    {
        if (_db is not null) await _db.DisposeAsync();
        _localLock.Dispose();
    }

    private sealed class EvaluationIdentity : ICurrentUserService
    {
        private string[] _permissions = [];
        public void SetPermissions(IEnumerable<string> permissions) => _permissions = permissions.ToArray();
        public bool IsAuthenticated => true;
        public Guid? UserId => ActorId;
        public Guid? TenantId => IsolatedEvaluationEnvironment.TenantId;
        public Guid? DepartmentId => IsolatedEvaluationEnvironment.DepartmentId;
        public string? SessionId => null;
        public string? Username => "eval-actor";
        public IReadOnlyCollection<string> Roles => ["EvaluationOperator"];
        public IReadOnlyCollection<string> PermissionCodes => _permissions;
        public bool IsSuperAdmin => false;
        public bool IsCurrentUserSuperAdmin() => false;
        public bool IsCurrentUserAdmin() => false;
        public bool CanManageBuiltinResources() => false;
        public bool HasPermission(string code) => PermissionEvaluation.HasPermission(true, false, _permissions, code);
    }
    private sealed class EvaluationConfiguration : IAiCenterConfiguration, IAiToolConfiguration
    {
        public bool Enabled => true;
        public IReadOnlyCollection<Guid> AllowedTenantIds => [TenantId];
        public int ConversationRetentionDays => 30;
        public int AuditRetentionDays => 180;
        public bool EnableReportDatasetTool => false;
        public IReadOnlyCollection<string> ApprovedReportDatasetKeys => [];
        public int MaxToolRows => 200;
    }
    private sealed class IsolatedUnitOfWork(AppDbContext db) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default) => action(cancellationToken);
    }
    private sealed class IsolatedTenantResolver : ITenantWriteResolver
    {
        public Guid ResolveTenantId(Guid? requestedTenantId = null) => requestedTenantId is null || requestedTenantId == TenantId
            ? TenantId : throw new EvaluationInputException("Evaluation cannot resolve another tenant for a write.");
    }
    private sealed class ProcessKeyProtector(string key) : IConfigValueProtector
    {
        public string Protect(string value) => throw new NotSupportedException("Evaluation does not persist credentials.");
        public string Unprotect(string protectedValue) => protectedValue == "evaluation-process-key" ? key : throw new EvaluationInputException("Unknown evaluation key reference.");
    }
    private sealed class IsolatedCancellationProbe : IAiRunCancellationProbe
    {
        public Task<bool> IsCancellationRequestedAsync(Guid runId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}

internal sealed class LocalLock : IDistributedLock, IDisposable
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    public async Task<DistributedLockHandle?> TryAcquireAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var gate = _locks.GetOrAdd(key, _ => new(1, 1));
        return await gate.WaitAsync(0, cancellationToken) ? new(key, "local-evaluation", expiry ?? TimeSpan.FromMinutes(2)) : null;
    }
    public async Task<DistributedLockHandle> AcquireAsync(string key, TimeSpan? expiry = null, TimeSpan? waitTime = null, CancellationToken cancellationToken = default)
    {
        var gate = _locks.GetOrAdd(key, _ => new(1, 1));
        if (!await gate.WaitAsync(waitTime ?? TimeSpan.FromSeconds(5), cancellationToken)) throw new TimeoutException("Isolated lock timed out.");
        return new(key, "local-evaluation", expiry ?? TimeSpan.FromMinutes(2));
    }
    public Task<bool> ReleaseAsync(DistributedLockHandle handle, CancellationToken cancellationToken = default)
    {
        _locks[handle.Key].Release();
        return Task.FromResult(true);
    }
    public async Task ExecuteWithLockAsync(string key, Func<CancellationToken, Task> action, TimeSpan? expiry = null, TimeSpan? waitTime = null, CancellationToken cancellationToken = default)
    {
        var handle = await AcquireAsync(key, expiry, waitTime, cancellationToken);
        try { await action(cancellationToken); } finally { await ReleaseAsync(handle, CancellationToken.None); }
    }
    public async Task<T> ExecuteWithLockAsync<T>(string key, Func<CancellationToken, Task<T>> action, TimeSpan? expiry = null, TimeSpan? waitTime = null, CancellationToken cancellationToken = default)
    {
        var handle = await AcquireAsync(key, expiry, waitTime, cancellationToken);
        try { return await action(cancellationToken); } finally { await ReleaseAsync(handle, CancellationToken.None); }
    }
    public void Dispose() { foreach (var gate in _locks.Values) gate.Dispose(); }
}
