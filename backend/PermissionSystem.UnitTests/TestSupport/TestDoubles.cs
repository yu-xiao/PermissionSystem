using System.Linq.Expressions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Excels;
using PermissionSystem.Application.Notifications;
using PermissionSystem.Application.Security;
using PermissionSystem.Application.Workflows;
using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.UnitTests.TestSupport;

internal static class TestIds
{
    public static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid AdminUserId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid NormalUserId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    public static readonly Guid ApproverUserId = Guid.Parse("30000000-0000-0000-0000-000000000003");
}

internal sealed class InMemoryRepository<TEntity> : IRepository<TEntity>
    where TEntity : BaseEntity
{
    private readonly List<TEntity> _items;

    public InMemoryRepository(params TEntity[] items)
    {
        _items = items.ToList();
    }

    public IReadOnlyList<TEntity> Items => _items;

    public IQueryable<TEntity> Query(bool ignoreQueryFilters = false)
    {
        return _items.Where(entity => !entity.IsDeleted).ToList().AsQueryable();
    }

    public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_items.FirstOrDefault(entity => entity.Id == id && !entity.IsDeleted));
    }

    public Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<TEntity>>(
            _items.Where(entity => !entity.IsDeleted).AsQueryable().Where(predicate).ToList());
    }

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        if (entity.TenantId == Guid.Empty)
        {
            entity.TenantId = TestIds.TenantId;
        }

        if (entity.CreatedAt == default)
        {
            entity.CreatedAt = DateTimeOffset.UtcNow;
        }

        _items.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(TEntity entity)
    {
    }

    public void Remove(TEntity entity)
    {
        entity.IsDeleted = true;
    }
}

internal sealed class TestUnitOfWork : IUnitOfWork
{
    public int SaveChangesCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        return Task.FromResult(0);
    }

    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        return action(cancellationToken);
    }
}

internal sealed class TestCurrentUserService : ICurrentUserService
{
    private readonly HashSet<string> _permissions;
    private readonly HashSet<string> _roles;

    public TestCurrentUserService(
        Guid? userId = null,
        bool isSuperAdmin = false,
        IEnumerable<string>? permissions = null,
        IEnumerable<string>? roles = null)
    {
        UserId = userId ?? TestIds.NormalUserId;
        IsSuperAdmin = isSuperAdmin;
        _permissions = (permissions ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _roles = (roles ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsAuthenticated => UserId.HasValue;
    public Guid? UserId { get; set; }
    public Guid? TenantId { get; set; } = TestIds.TenantId;
    public Guid? DepartmentId { get; set; }
    public string? SessionId { get; set; } = "test-session";
    public string? Username { get; set; } = "tester";
    public IReadOnlyCollection<string> Roles => _roles;
    public IReadOnlyCollection<string> PermissionCodes => _permissions;
    public bool IsSuperAdmin { get; set; }

    public bool IsCurrentUserSuperAdmin() => IsSuperAdmin;
    public bool IsCurrentUserAdmin() => IsSuperAdmin;
    public bool CanManageBuiltinResources() => IsSuperAdmin;

    public bool HasPermission(string permissionCode)
    {
        return IsSuperAdmin || _permissions.Contains(permissionCode);
    }
}

internal sealed class TestTenantWriteResolver : ITenantWriteResolver
{
    private readonly Guid _tenantId;

    public TestTenantWriteResolver(Guid? tenantId = null)
    {
        _tenantId = tenantId ?? TestIds.TenantId;
    }

    public Guid ResolveTenantId(Guid? requestedTenantId = null)
    {
        return requestedTenantId is { } value && value != Guid.Empty ? value : _tenantId;
    }
}

internal sealed class TestCacheService : ICacheService
{
    private readonly Dictionary<string, object?> _items = new(StringComparer.Ordinal);

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_items.TryGetValue(key, out var value) ? (T?)value : default);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_items.ContainsKey(key));
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpirationRelativeToNow = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default)
    {
        _items[key] = value;
        return Task.CompletedTask;
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpirationRelativeToNow = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default)
    {
        if (_items.TryGetValue(key, out var value))
        {
            return (T)value!;
        }

        var created = await factory(cancellationToken);
        _items[key] = created;
        return created;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _items.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        foreach (var key in _items.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
        {
            _items.Remove(key);
        }

        return Task.CompletedTask;
    }

    public Task RefreshAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

internal sealed class TestSecurityPolicyService : ISecurityPolicyService
{
    public List<(string OperationCode, bool Force)> VerificationRequests { get; } = [];

    public Task<SecurityPolicyResponse> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SecurityPolicyResponse());
    }

    public Task<SecurityPolicyResponse> UpdatePolicyAsync(UpdateSecurityPolicyRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SecurityPolicyResponse());
    }

    public Task ValidatePasswordAsync(string password, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task EnsureLoginAllowedAsync(string userName, string? ipAddress, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task RecordLoginFailureAsync(Guid tenantId, string userName, string? ipAddress, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task ClearLoginFailureAsync(Guid tenantId, string userName, string? ipAddress, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<SendSensitiveVerificationResponse> SendVerificationAsync(SendSensitiveVerificationRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SendSensitiveVerificationResponse
        {
            OperationCode = request.OperationCode,
            VerifyCode = "123456",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        });
    }

    public Task VerifyAsync(VerifySensitiveOperationRequest request, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task EnsureSensitiveOperationVerifiedAsync(string operationCode, CancellationToken cancellationToken = default)
    {
        VerificationRequests.Add((operationCode, false));
        return Task.CompletedTask;
    }

    public Task EnsureSensitiveOperationVerifiedAsync(string operationCode, bool force, CancellationToken cancellationToken = default)
    {
        VerificationRequests.Add((operationCode, force));
        return Task.CompletedTask;
    }

    public Task<bool> IsIpAllowedAsync(string? ipAddress, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<PagedResult<IpAccessRuleResponse>> GetIpRulesAsync(IpAccessRuleQueryRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PagedResult<IpAccessRuleResponse>.Create([], request.PageIndex, request.PageSize, 0));
    }

    public Task<IpAccessRuleResponse> CreateIpRuleAsync(CreateIpAccessRuleRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new IpAccessRuleResponse());
    }

    public Task<IpAccessRuleResponse> UpdateIpRuleAsync(Guid id, UpdateIpAccessRuleRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new IpAccessRuleResponse());
    }

    public Task DeleteIpRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<PagedResult<LoginFailureRecordResponse>> GetLoginFailuresAsync(LoginFailureQueryRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PagedResult<LoginFailureRecordResponse>.Create([], request.PageIndex, request.PageSize, 0));
    }
}

internal sealed class TestPasswordHashService : IPasswordHashService
{
    public string HashPassword(string password) => "hashed:" + password;

    public bool VerifyPassword(string passwordHash, string password)
    {
        return passwordHash == HashPassword(password);
    }
}

internal sealed class TestConfigValueProtector : IConfigValueProtector
{
    public string Protect(string value) => "protected:" + value;

    public string Unprotect(string protectedValue)
    {
        return protectedValue.StartsWith("protected:", StringComparison.Ordinal)
            ? protectedValue["protected:".Length..]
            : protectedValue;
    }
}

internal sealed class TestExcelService : IExcelService
{
    public Task<byte[]> ExportAsync<T>(ExportRequest<T> request, CancellationToken cancellationToken = default)
        where T : class
    {
        return Task.FromResult(Array.Empty<byte>());
    }

    public Task<byte[]> ExportTableAsync(ExportTableRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Array.Empty<byte>());
    }

    public Task<ImportResult<T>> ImportAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return Task.FromResult(new ImportResult<T>());
    }

    public Task<byte[]> CreateTemplateAsync<T>(string sheetName, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return Task.FromResult(Array.Empty<byte>());
    }
}

internal sealed class TestNotificationService : INotificationService
{
    public List<SendSystemNotificationRequest> Sent { get; } = [];

    public Task<PagedResult<NotificationResponse>> GetMyNotificationsAsync(NotificationQueryRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PagedResult<NotificationResponse>.Create([], request.PageIndex, request.PageSize, 0));
    }

    public Task<int> GetMyUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    public Task MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteMineAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SendSystemNotificationAsync(SendSystemNotificationRequest request, CancellationToken cancellationToken = default)
    {
        Sent.Add(request);
        return Task.CompletedTask;
    }

    public Task HandleNotificationEventAsync(NotificationCreatedEvent notificationEvent, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<PagedResult<NotificationTemplateResponse>> GetTemplatesAsync(NotificationTemplateQueryRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PagedResult<NotificationTemplateResponse>.Create([], request.PageIndex, request.PageSize, 0));
    }

    public Task<NotificationTemplateResponse> CreateTemplateAsync(SaveNotificationTemplateRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new NotificationTemplateResponse());
    }

    public Task<NotificationTemplateResponse> UpdateTemplateAsync(Guid id, SaveNotificationTemplateRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new NotificationTemplateResponse());
    }

    public Task DeleteTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

internal sealed class TestWorkflowBusinessHandlerResolver : IWorkflowBusinessHandlerResolver
{
    private readonly IWorkflowBusinessHandler _handler;

    public TestWorkflowBusinessHandlerResolver(IWorkflowBusinessHandler handler)
    {
        _handler = handler;
    }

    public IWorkflowBusinessHandler Resolve(string businessType)
    {
        return _handler;
    }
}

internal sealed class TestWorkflowBusinessHandler : IWorkflowBusinessHandler
{
    public string BusinessType => "Demo";

    public List<WorkflowActionType> Actions { get; } = [];

    public Task OnWorkflowStartedAsync(WorkflowBusinessContext context, CancellationToken cancellationToken)
    {
        Actions.Add(WorkflowActionType.Start);
        return Task.CompletedTask;
    }

    public Task OnWorkflowApprovedAsync(WorkflowBusinessContext context, CancellationToken cancellationToken)
    {
        Actions.Add(WorkflowActionType.Approve);
        return Task.CompletedTask;
    }

    public Task OnWorkflowRejectedAsync(WorkflowBusinessContext context, CancellationToken cancellationToken)
    {
        Actions.Add(WorkflowActionType.Reject);
        return Task.CompletedTask;
    }

    public Task OnWorkflowWithdrawnAsync(WorkflowBusinessContext context, CancellationToken cancellationToken)
    {
        Actions.Add(WorkflowActionType.Withdraw);
        return Task.CompletedTask;
    }

    public Task OnWorkflowCancelledAsync(WorkflowBusinessContext context, CancellationToken cancellationToken)
    {
        Actions.Add(WorkflowActionType.System);
        return Task.CompletedTask;
    }
}
