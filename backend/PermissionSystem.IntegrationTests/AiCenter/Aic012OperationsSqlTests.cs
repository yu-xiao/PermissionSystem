using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Authentication;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Queries;
using PermissionSystem.Infrastructure.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class Aic012OperationsSqlTests
{
    private const string ConnectionVariable = "PERMISSION_SYSTEM_AIC012_SQL_TEST_CONNECTION";
    private const string IsolatedVariable = "PERMISSION_SYSTEM_AIC012_SQL_TEST_ISOLATED";

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task Statistics_MatchSyntheticPopulationAndUseCurrentDatabasePermissions()
    {
        await using var f = await SqlFixture.CreateAsync();
        var result = await f.Service().QueryAsync(f.Request);
        var row = Assert.Single(result.Scenarios.Items);
        Assert.Equal(2, row.RunCount); Assert.Equal(50m, row.TechnicalCompletionRate);
        Assert.Equal(1, row.TimeoutFailureCount); Assert.Equal(200, row.P95DurationMilliseconds);
        Assert.Equal(100m, row.FeedbackCoverageRate); Assert.Equal(100m, row.PositiveFeedbackRate);
        Assert.Equal(15, row.InputTokens); Assert.Equal(25, row.OutputTokens);
        Assert.Collection(row.EstimatedCosts,
            c => { Assert.Equal("CNY", c.Currency); Assert.Equal(2m, c.Amount); },
            c => { Assert.Equal("USD", c.Currency); Assert.Equal(1m, c.Amount); });
    }

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task Statistics_ExcludeDeletedPopulationAndWrongTenantOrOwnerAssociations()
    {
        await using var f = await SqlFixture.CreateAsync();
        var target = Guid.NewGuid(); var otherTenant = new TenantContext(); otherTenant.SetTenant(target, "Synthetic AIC-012 SQL fixture");
        await using var other = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(f.Db.Database.GetDbConnection()).Options, otherTenant, new NullAuditContext());
        await other.Database.UseTransactionAsync(f.Transaction.GetDbTransaction());
        var otherUser = new User { Id = Guid.NewGuid(), TenantId = target, UserName = "synthetic-other",
            NormalizedUserName = "SYNTHETIC-OTHER", DisplayName = "Synthetic other actor" };
        other.Tenants.Add(new() { Id = target, TenantId = target, Code = $"aic012-{target:N}", Name = "Synthetic other tenant", Status = TenantStatus.Active });
        other.Users.Add(otherUser);
        // Existing usage/feedback foreign keys use RunId; readers must also enforce their TenantId.
        other.AiUsageLogs.Add(new() { TenantId = target, RunId = f.Completed.Id, ProviderConfigId = f.Provider.Id,
            Sequence = 1, Status = AiInvocationStatus.Completed, InputTokens = 900, OutputTokens = 900, EstimatedCost = 900, PricingCurrency = "USD" });
        await other.SaveChangesAsync();
        f.Db.AiUserFeedbacks.Add(new() { TenantId = f.Tenant.TenantId!.Value, RunId = f.Completed.Id,
            UserId = otherUser.Id, MessageId = f.Completed.ResponseMessageId!.Value, Rating = AiFeedbackRating.Negative });
        var deleted = f.Run(AiRunStatus.Completed, 900);
        f.Db.AiRuns.Add(deleted); await f.Db.SaveChangesAsync();
        f.Db.AiRuns.Remove(deleted); await f.Db.SaveChangesAsync();
        var row = Assert.Single((await f.Service().QueryAsync(f.Request)).Scenarios.Items);
        Assert.Equal(2, row.RunCount); Assert.Equal(1, row.PositiveFeedbackCount); Assert.Equal(0, row.NegativeFeedbackCount);
        Assert.Equal(15, row.InputTokens); Assert.Equal(1m, row.EstimatedCosts.Single(c => c.Currency == "USD").Amount);
    }

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task Statistics_RejectDatabaseRoleRevocationDuringObservation()
    {
        await using var f = await SqlFixture.CreateAsync();
        var executor = new RevokeExecutor(f.Db, f.Role.Id);
        var error = await Assert.ThrowsAsync<BusinessException>(() => f.Service(executor).QueryAsync(f.Request));
        Assert.Equal(ErrorCode.Forbidden, error.ErrorCode);
    }

    internal sealed class SqlFactAttribute : FactAttribute
    {
        public SqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)) ||
                Environment.GetEnvironmentVariable(IsolatedVariable) != "1")
                Skip = "Requires an explicitly isolated AIC-012 SQL Server with reviewed/applied migrations. Only rolled-back synthetic fixtures; no migrations are applied.";
        }
    }

    internal sealed class SqlFixture : IAsyncDisposable
    {
        public TenantContext Tenant { get; } = new();
        public AppDbContext Db { get; }
        public IDbContextTransaction Transaction { get; private set; } = null!;
        public User Actor { get; } = new() { Id = Guid.NewGuid(), UserName = "synthetic-actor", NormalizedUserName = "SYNTHETIC-ACTOR", DisplayName = "Synthetic actor" };
        public Role Role { get; } = new() { Id = Guid.NewGuid(), Code = "synthetic-operations", Name = "Synthetic operations reader", IsEnabled = true };
        public AiProviderConfig Provider { get; } = new() { Id = Guid.NewGuid(), ProviderCode = "synthetic", ProviderName = "Synthetic provider",
            BaseUrl = "https://example.invalid", ModelName = "synthetic", IsEnabled = false };
        private readonly Guid _conversationId = Guid.NewGuid();
        private readonly Guid _requestMessageId = Guid.NewGuid();
        private readonly Guid _responseMessageId = Guid.NewGuid();
        public AiRun Completed { get; private set; } = null!;
        public AiScenarioOperationsQueryRequest Request => new() { From = DateTimeOffset.UtcNow.AddDays(-1), To = DateTimeOffset.UtcNow.AddMinutes(1) };

        private SqlFixture()
        {
            Tenant.SetTenant(Guid.NewGuid(), "Synthetic AIC-012 SQL fixture");
            Db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(Environment.GetEnvironmentVariable(ConnectionVariable)!).Options,
                Tenant, new NullAuditContext());
        }

        public static async Task<SqlFixture> CreateAsync()
        {
            var f = new SqlFixture();
            try
            {
                Assert.Empty(await f.Db.Database.GetPendingMigrationsAsync());
                f.Transaction = await f.Db.Database.BeginTransactionAsync(); var tenantId = f.Tenant.TenantId!.Value;
                f.Actor.TenantId = f.Role.TenantId = f.Provider.TenantId = tenantId;
                f.Db.Tenants.Add(new() { Id = tenantId, TenantId = tenantId, Code = $"aic012-{tenantId:N}", Name = "Synthetic AIC-012 tenant", Status = TenantStatus.Active });
                f.Db.Users.Add(f.Actor); f.Db.Roles.Add(f.Role); f.Db.AiProviderConfigs.Add(f.Provider);
                var permission = new Permission { Id = Guid.NewGuid(), TenantId = tenantId, Code = AiCenterConstants.OperationsViewPermission, Name = "Synthetic operations view" };
                f.Db.Permissions.Add(permission);
                f.Db.UserRoles.Add(new() { TenantId = tenantId, UserId = f.Actor.Id, RoleId = f.Role.Id });
                f.Db.RolePermissions.Add(new() { TenantId = tenantId, RoleId = f.Role.Id, PermissionId = permission.Id });
                f.Db.AiConversations.Add(new() { Id = f._conversationId, TenantId = tenantId, UserId = f.Actor.Id,
                    AgentCode = "synthetic", AgentVersion = "1", Title = "Synthetic fixture", LastMessageAt = DateTimeOffset.UtcNow, RetentionUntil = DateTimeOffset.UtcNow.AddDays(1) });
                f.Db.AiMessages.AddRange(new AiMessage { Id = f._requestMessageId, TenantId = tenantId, ConversationId = f._conversationId,
                    Role = AiMessageRole.User, Sequence = 1, Content = "Synthetic question", ContentDigest = "synthetic" },
                    new AiMessage { Id = f._responseMessageId, TenantId = tenantId, ConversationId = f._conversationId,
                        Role = AiMessageRole.Assistant, Sequence = 2, Content = "Synthetic answer", ContentDigest = "synthetic" });
                await f.Db.SaveChangesAsync();
                f.Completed = f.Run(AiRunStatus.Completed, 25); var failed = f.Run(AiRunStatus.Failed, 200); failed.ErrorCode = "run_queue_timeout";
                f.Db.AiRuns.AddRange(f.Completed, failed); await f.Db.SaveChangesAsync();
                f.Db.AiUsageLogs.AddRange(new AiUsageLog { TenantId = tenantId, RunId = f.Completed.Id, ProviderConfigId = f.Provider.Id,
                    Sequence = 1, Status = AiInvocationStatus.Completed, InputTokens = 10, OutputTokens = 20, EstimatedCost = 1, PricingCurrency = "USD" },
                    new AiUsageLog { TenantId = tenantId, RunId = failed.Id, ProviderConfigId = f.Provider.Id,
                        Sequence = 1, Status = AiInvocationStatus.Failed, InputTokens = 5, OutputTokens = 5, EstimatedCost = 2, PricingCurrency = "CNY" });
                f.Db.AiUserFeedbacks.Add(new() { TenantId = tenantId, RunId = f.Completed.Id, UserId = f.Actor.Id,
                    MessageId = f._responseMessageId, Rating = AiFeedbackRating.Positive });
                await f.Db.SaveChangesAsync(); return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }

        public AiRun Run(AiRunStatus status, long duration) => new()
        {
            Id = Guid.NewGuid(), TenantId = Tenant.TenantId!.Value, ConversationId = _conversationId, ProviderConfigId = Provider.Id,
            ActorUserId = Actor.Id, RequestMessageId = _requestMessageId, ResponseMessageId = status == AiRunStatus.Completed ? _responseMessageId : null,
            Status = status, AgentCode = "synthetic", AgentVersion = "1", PromptVersion = "1", ModelName = "synthetic",
            TraceId = "synthetic", DurationMilliseconds = duration, ExecutionLeaseId = Guid.NewGuid()
        };
        public AiScenarioOperationsService Service(IAsyncQueryExecutor? executor = null) => new(
            new Repository<AiRun>(Db), new Repository<AiUsageLog>(Db), new Repository<AiUserFeedback>(Db), new Repository<AiScenario>(Db),
            executor ?? new EfCoreAsyncQueryExecutor(), new SqlCurrentUser(Actor, Role), Tenant, new UserCredentialValidator(Db, new PasswordHasher<User>(), Tenant));
        public async ValueTask DisposeAsync()
        { if (Transaction is not null) await Transaction.DisposeAsync(); await Db.DisposeAsync(); }
    }

    internal sealed class SqlCurrentUser(User actor, Role role, bool export = false) : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => actor.Id;
        public Guid? TenantId => actor.TenantId;
        public Guid? DepartmentId => null;
        public string? SessionId => null;
        public Guid? SecurityStamp => actor.SecurityStamp;
        public string? Username => "Synthetic actor";
        public IReadOnlyCollection<string> Roles => [role.Code];
        public IReadOnlyCollection<string> PermissionCodes => export
            ? [AiCenterConstants.OperationsViewPermission, AiCenterConstants.OperationsExportPermission]
            : [AiCenterConstants.OperationsViewPermission];
        public bool IsSuperAdmin => false;
        public bool IsCurrentUserSuperAdmin() => false;
        public bool IsCurrentUserAdmin() => false;
        public bool CanManageBuiltinResources() => false;
        public bool HasPermission(string code) => PermissionCodes.Contains(code);
    }

    internal sealed class RevokeExecutor(AppDbContext db, Guid roleId) : IAsyncQueryExecutor
    {
        private readonly EfCoreAsyncQueryExecutor _inner = new(); private bool _revoked;
        public async Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken ct = default)
        {
            var result = await _inner.ToListAsync(query, ct);
            if (!_revoked) { _revoked = true; await db.Roles.Where(r => r.Id == roleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsEnabled, false), ct); }
            return result;
        }
        public Task<long> LongCountAsync<T>(IQueryable<T> query, CancellationToken ct = default) => _inner.LongCountAsync(query, ct);
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct = default) => _inner.AnyAsync(query, ct);
        public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct = default) => _inner.FirstOrDefaultAsync(query, ct);
    }
}
